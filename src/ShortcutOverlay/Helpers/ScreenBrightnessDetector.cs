using System.IO;
using System.Runtime.InteropServices;
using ShortcutOverlay.NativeInterop;

namespace ShortcutOverlay.Helpers;

/// <summary>
/// Production-quality background brightness detector.
///
/// Strategy: Captures a region of the foreground window's SCREEN AREA that does NOT
/// overlap with our overlay, using BitBlt from the desktop DC. This approach:
///   1. Works with GPU-rendered windows (Electron, Chrome, UWP, games)
///   2. Avoids capturing our own overlay pixels (self-sampling problem)
///   3. Is fast (~1-3ms per capture) via GetDIBits bulk pixel read
///
/// PrintWindow was abandoned because it returns black bitmaps for hardware-
/// accelerated (GPU-rendered) windows like Discord, WhatsApp, Chrome, etc.
///
/// Uses hysteresis to prevent rapid toggling near the brightness threshold.
/// </summary>
public static class ScreenBrightnessDetector
{
    // Hysteresis thresholds — wide dead zone prevents flickering
    private const double UpperThreshold = 0.58;
    private const double LowerThreshold = 0.42;

    // Size of the sample area (pixels). 200x200 = 400 samples at step=10.
    private const int SampleSize = 200;

    private static bool? _lastDecisionIsLight;

    // Debug log path (only written when enabled)
    private static readonly string DebugLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ShortcutOverlay", "adaptive_debug.log");
    private static bool _debugLogging = false;
    private static int _debugLogCount = 0;

    /// <summary>
    /// Enable/disable debug logging. When enabled, writes brightness values
    /// to %AppData%/ShortcutOverlay/adaptive_debug.log (max 200 entries).
    /// </summary>
    public static bool DebugLogging
    {
        get => _debugLogging;
        set
        {
            _debugLogging = value;
            if (value)
            {
                _debugLogCount = 0;
                try { File.WriteAllText(DebugLogPath, $"=== Adaptive Debug Log Started {DateTime.Now:s} ===\n"); }
                catch { /* ignore */ }
            }
        }
    }

    /// <summary>
    /// Detects whether the background behind the overlay is light or dark.
    /// Samples the foreground window's visible screen area, avoiding the overlay region.
    /// </summary>
    public static bool IsBackgroundLight(IntPtr foregroundHwnd,
        int overlayScreenX, int overlayScreenY, int overlayW, int overlayH)
    {
        if (foregroundHwnd == IntPtr.Zero)
            return ApplyHysteresis(0.5);

        // Get the foreground window's screen rect
        if (!Win32Api.GetWindowRect(foregroundHwnd, out var winRect))
            return ApplyHysteresis(0.5);

        int winW = winRect.Width;
        int winH = winRect.Height;
        if (winW <= 0 || winH <= 0)
            return ApplyHysteresis(0.5);

        // Find a sample region within the foreground window that does NOT
        // overlap with our overlay. We try several candidate positions.
        var sampleRect = FindNonOverlappingSampleRegion(
            winRect, overlayScreenX, overlayScreenY, overlayW, overlayH);

        if (sampleRect.w <= 0 || sampleRect.h <= 0)
        {
            // Overlay covers entire foreground window? Fall back to edge strips.
            double fallback = GetBrightnessViaScreenStrips(
                overlayScreenX, overlayScreenY, overlayW, overlayH);
            LogDebug($"Fallback strips: brightness={fallback:F3}");
            return ApplyHysteresis(fallback);
        }

        // Capture that region from the SCREEN (not the window) via BitBlt
        double brightness = CaptureAndAnalyzeScreenRegion(
            sampleRect.x, sampleRect.y, sampleRect.w, sampleRect.h);

        LogDebug($"BitBlt region=({sampleRect.x},{sampleRect.y},{sampleRect.w},{sampleRect.h}) " +
                 $"brightness={brightness:F3} last={_lastDecisionIsLight}");

        if (brightness < 0)
        {
            // BitBlt failed, use strip fallback
            brightness = GetBrightnessViaScreenStrips(
                overlayScreenX, overlayScreenY, overlayW, overlayH);
        }

        return ApplyHysteresis(brightness);
    }

    /// <summary>
    /// Finds a SampleSize x SampleSize region within the foreground window's screen rect
    /// that does NOT overlap with the overlay. Tries center first, then four quadrants.
    /// </summary>
    private static (int x, int y, int w, int h) FindNonOverlappingSampleRegion(
        Win32Api.RECT winRect, int oX, int oY, int oW, int oH)
    {
        int sw = Math.Min(SampleSize, winRect.Width);
        int sh = Math.Min(SampleSize, winRect.Height);

        // Candidate sample positions (top-left corner of the sample area)
        // Priority: center of the foreground window, then four quadrants
        var candidates = new (int x, int y)[]
        {
            // Center of the foreground window
            (winRect.Left + (winRect.Width - sw) / 2, winRect.Top + (winRect.Height - sh) / 2),
            // Top-left quarter
            (winRect.Left + winRect.Width / 4 - sw / 2, winRect.Top + winRect.Height / 4 - sh / 2),
            // Top-right quarter
            (winRect.Left + 3 * winRect.Width / 4 - sw / 2, winRect.Top + winRect.Height / 4 - sh / 2),
            // Bottom-left quarter
            (winRect.Left + winRect.Width / 4 - sw / 2, winRect.Top + 3 * winRect.Height / 4 - sh / 2),
            // Bottom-right quarter
            (winRect.Left + 3 * winRect.Width / 4 - sw / 2, winRect.Top + 3 * winRect.Height / 4 - sh / 2),
            // Far left edge
            (winRect.Left + 20, winRect.Top + winRect.Height / 2 - sh / 2),
            // Far right edge
            (winRect.Right - sw - 20, winRect.Top + winRect.Height / 2 - sh / 2),
        };

        foreach (var (cx, cy) in candidates)
        {
            // Clamp to foreground window bounds
            int sx = Math.Max(winRect.Left, Math.Min(cx, winRect.Right - sw));
            int sy = Math.Max(winRect.Top, Math.Min(cy, winRect.Bottom - sh));

            // Check if this sample area does NOT overlap with the overlay
            if (!RectsOverlap(sx, sy, sw, sh, oX, oY, oW, oH))
                return (sx, sy, sw, sh);
        }

        // All candidates overlap — try to find ANY non-overlapping strip
        // Try a horizontal strip above the overlay
        if (oY - winRect.Top > 50)
        {
            int stripH = Math.Min(50, oY - winRect.Top);
            return (winRect.Left + 20, oY - stripH, Math.Min(winRect.Width - 40, 400), stripH);
        }

        // Try below the overlay
        if (winRect.Bottom - (oY + oH) > 50)
        {
            int stripH = Math.Min(50, winRect.Bottom - (oY + oH));
            return (winRect.Left + 20, oY + oH, Math.Min(winRect.Width - 40, 400), stripH);
        }

        // Try left of the overlay
        if (oX - winRect.Left > 50)
        {
            int stripW = Math.Min(50, oX - winRect.Left);
            return (winRect.Left + 10, winRect.Top + 50, stripW, Math.Min(winRect.Height - 100, 400));
        }

        // Give up — overlay covers the entire window
        return (0, 0, 0, 0);
    }

    private static bool RectsOverlap(int ax, int ay, int aw, int ah, int bx, int by, int bw, int bh)
    {
        return ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by;
    }

    /// <summary>
    /// Captures a screen region via BitBlt and analyzes its brightness.
    /// This captures what's ACTUALLY visible on screen — works with GPU-rendered windows.
    /// Returns brightness 0..1, or -1 on failure.
    /// </summary>
    private static double CaptureAndAnalyzeScreenRegion(int x, int y, int w, int h)
    {
        if (w <= 0 || h <= 0) return -1;

        var hdcScreen = Win32Api.GetDC(IntPtr.Zero); // Desktop DC
        if (hdcScreen == IntPtr.Zero) return -1;

        var hdcMem = Win32Api.CreateCompatibleDC(hdcScreen);
        var hBitmap = Win32Api.CreateCompatibleBitmap(hdcScreen, w, h);
        var hOld = Win32Api.SelectObject(hdcMem, hBitmap);

        // Capture from screen — this always works, captures actual visible pixels
        Win32Api.BitBlt(hdcMem, 0, 0, w, h, hdcScreen, x, y, Win32Api.SRCCOPY);
        Win32Api.ReleaseDC(IntPtr.Zero, hdcScreen);

        // Analyze the captured bitmap
        double brightness = AnalyzeRegionFromDC(hdcMem, hBitmap, 0, 0, w, h);

        Win32Api.SelectObject(hdcMem, hOld);
        Win32Api.DeleteObject(hBitmap);
        Win32Api.DeleteDC(hdcMem);

        return brightness;
    }

    /// <summary>
    /// Fallback: samples thin strips around overlay edges from the screen.
    /// Used when the overlay covers the entire foreground window.
    /// </summary>
    private static double GetBrightnessViaScreenStrips(
        int overlayX, int overlayY, int overlayW, int overlayH)
    {
        const int stripW = 40;

        var hdcScreen = Win32Api.GetDC(IntPtr.Zero);
        if (hdcScreen == IntPtr.Zero) return 0.5;

        var strips = new (int x, int y, int w, int h)[]
        {
            (Math.Max(0, overlayX - stripW), overlayY, stripW, overlayH),  // Left of overlay
            (overlayX + overlayW, overlayY, stripW, overlayH),              // Right of overlay
            (overlayX, Math.Max(0, overlayY - stripW), overlayW, stripW),  // Above overlay
            (overlayX, overlayY + overlayH, overlayW, stripW),              // Below overlay
        };

        double totalLum = 0;
        long totalSamples = 0;

        foreach (var (sx, sy, sw, sh) in strips)
        {
            if (sw <= 0 || sh <= 0) continue;

            var hdcMem = Win32Api.CreateCompatibleDC(hdcScreen);
            var hBmp = Win32Api.CreateCompatibleBitmap(hdcScreen, sw, sh);
            var hOldBmp = Win32Api.SelectObject(hdcMem, hBmp);

            Win32Api.BitBlt(hdcMem, 0, 0, sw, sh, hdcScreen, sx, sy, Win32Api.SRCCOPY);

            double stripBrightness = AnalyzeRegionFromDC(hdcMem, hBmp, 0, 0, sw, sh);
            if (stripBrightness >= 0)
            {
                totalLum += stripBrightness;
                totalSamples++;
            }

            Win32Api.SelectObject(hdcMem, hOldBmp);
            Win32Api.DeleteObject(hBmp);
            Win32Api.DeleteDC(hdcMem);
        }

        Win32Api.ReleaseDC(IntPtr.Zero, hdcScreen);
        return totalSamples > 0 ? totalLum / totalSamples : 0.5;
    }

    /// <summary>
    /// Fast pixel analysis using GetDIBits. Reads a bitmap region
    /// and returns average perceived brightness (0..1). Returns -1 on failure.
    /// Samples every 10th pixel for speed.
    /// </summary>
    private static double AnalyzeRegionFromDC(IntPtr hdcMem, IntPtr hBitmap,
        int regionX, int regionY, int regionW, int regionH)
    {
        if (regionW <= 0 || regionH <= 0) return -1;

        var bmi = new Win32Api.BITMAPINFO();
        bmi.bmiHeader.biSize = (uint)Marshal.SizeOf<Win32Api.BITMAPINFOHEADER>();

        // First call to get bitmap dimensions
        Win32Api.GetDIBits(hdcMem, hBitmap, 0, 0, IntPtr.Zero, ref bmi, Win32Api.DIB_RGB_COLORS);

        int fullW = Math.Abs(bmi.bmiHeader.biWidth);
        int fullH = Math.Abs(bmi.bmiHeader.biHeight);
        if (fullW <= 0 || fullH <= 0) return -1;

        // Set up for 32bpp top-down extraction
        bmi.bmiHeader.biWidth = fullW;
        bmi.bmiHeader.biHeight = -fullH; // Negative = top-down
        bmi.bmiHeader.biPlanes = 1;
        bmi.bmiHeader.biBitCount = 32;
        bmi.bmiHeader.biCompression = 0; // BI_RGB

        int bufferSize = fullW * fullH * 4;
        var buffer = Marshal.AllocHGlobal(bufferSize);

        try
        {
            int result = Win32Api.GetDIBits(hdcMem, hBitmap, 0, (uint)fullH,
                buffer, ref bmi, Win32Api.DIB_RGB_COLORS);
            if (result <= 0) return -1;

            double totalLum = 0;
            int sampleCount = 0;
            int step = 10;

            unsafe
            {
                byte* ptr = (byte*)buffer.ToPointer();

                for (int py = regionY; py < regionY + regionH && py < fullH; py += step)
                {
                    int rowOffset = py * fullW * 4;
                    for (int px = regionX; px < regionX + regionW && px < fullW; px += step)
                    {
                        int offset = rowOffset + px * 4;
                        // DIB format: B G R A
                        byte b = ptr[offset];
                        byte g = ptr[offset + 1];
                        byte r = ptr[offset + 2];

                        // ITU-R BT.709 perceived luminance
                        double lum = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255.0;
                        totalLum += lum;
                        sampleCount++;
                    }
                }
            }

            return sampleCount > 0 ? totalLum / sampleCount : -1;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>
    /// Applies hysteresis: only flips the decision if brightness crosses
    /// the far threshold, preventing rapid toggling.
    /// </summary>
    private static bool ApplyHysteresis(double brightness)
    {
        if (_lastDecisionIsLight == null)
        {
            _lastDecisionIsLight = brightness >= 0.50;
            LogDebug($"Initial decision: brightness={brightness:F3} isLight={_lastDecisionIsLight}");
            return _lastDecisionIsLight.Value;
        }

        bool oldDecision = _lastDecisionIsLight.Value;

        if (_lastDecisionIsLight.Value)
        {
            // Currently "light" — only switch to "dark" if below lower threshold
            if (brightness < LowerThreshold)
                _lastDecisionIsLight = false;
        }
        else
        {
            // Currently "dark" — only switch to "light" if above upper threshold
            if (brightness > UpperThreshold)
                _lastDecisionIsLight = true;
        }

        if (oldDecision != _lastDecisionIsLight.Value)
            LogDebug($"Decision CHANGED: brightness={brightness:F3} isLight={_lastDecisionIsLight}");

        return _lastDecisionIsLight.Value;
    }

    public static void ResetState() => _lastDecisionIsLight = null;

    private static void LogDebug(string message)
    {
        if (!_debugLogging || _debugLogCount > 200) return;
        _debugLogCount++;
        try
        {
            File.AppendAllText(DebugLogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
        }
        catch { /* ignore */ }
    }
}
