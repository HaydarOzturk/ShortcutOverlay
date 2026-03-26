using System.Runtime.InteropServices;
using ShortcutOverlay.NativeInterop;

namespace ShortcutOverlay.Helpers;

/// <summary>
/// Background brightness detector that samples the screen area AROUND the overlay.
///
/// Strategy: Sample 4 strips immediately adjacent to the overlay (left, right, above, below).
/// This tells us exactly what's visible behind the overlay, not some distant region of the
/// foreground window. Handles mixed backgrounds (e.g., Claude light sidebar + terminal dark)
/// by weighting the strips closest to the overlay center.
///
/// Also returns brightness variance — high variance means mixed content (text on background),
/// which the overlay can use to boost opacity for better readability.
///
/// Strips are clamped to screen work area (excludes taskbar) to avoid sampling
/// the taskbar which is always dark and skews brightness detection.
/// </summary>
public static class ScreenBrightnessDetector
{
    // Hysteresis thresholds — wider dead zone to prevent flip-flopping
    private const double UpperThreshold = 0.60;
    private const double LowerThreshold = 0.40;

    // Strip width for sampling around overlay edges
    private const int StripSize = 80;

    private static bool? _lastDecisionIsLight;

    // Exposed for readability adjustments
    public static double LastBrightnessVariance { get; private set; }
    public static double LastBrightness { get; private set; }

    /// <summary>
    /// Detects whether the background directly around the overlay is light or dark.
    /// Samples the screen strips immediately adjacent to the overlay boundaries.
    /// </summary>
    public static bool IsBackgroundLight(IntPtr foregroundHwnd,
        int overlayScreenX, int overlayScreenY, int overlayW, int overlayH)
    {
        // Sample strips around the overlay — this captures what's ACTUALLY behind it
        var (brightness, variance) = SampleAroundOverlay(
            overlayScreenX, overlayScreenY, overlayW, overlayH);

        LastBrightness = brightness;
        LastBrightnessVariance = variance;

        AdaptiveDebugLog.Log($"Brightness: local={brightness:F3}, variance={variance:F3}");

        return ApplyHysteresis(brightness);
    }

    /// <summary>
    /// Samples 4 strips immediately adjacent to the overlay and returns
    /// average brightness + variance. The strips sample what's visible on screen
    /// right next to where the overlay sits.
    ///
    /// All strips are clamped to the screen work area (excludes taskbar).
    /// </summary>
    private static (double brightness, double variance) SampleAroundOverlay(
        int oX, int oY, int oW, int oH)
    {
        var hdcScreen = Win32Api.GetDC(IntPtr.Zero);
        if (hdcScreen == IntPtr.Zero) return (0.5, 0);

        // Get the work area (screen minus taskbar)
        GetWorkArea(out int screenLeft, out int screenTop, out int screenRight, out int screenBottom);

        // Four strips surrounding the overlay, clamped to work area:
        //   [  above  ]
        //   [L]      [R]
        //   [  below  ]
        var strips = new List<(int x, int y, int w, int h)>();

        // Left strip
        int leftW = Math.Min(StripSize, oX - screenLeft);
        if (leftW > 10)
            strips.Add((oX - leftW, oY, leftW, oH));

        // Right strip
        int rightX = oX + oW;
        int rightW = Math.Min(StripSize, screenRight - rightX);
        if (rightW > 10)
            strips.Add((rightX, oY, rightW, oH));

        // Above strip
        int aboveH = Math.Min(StripSize, oY - screenTop);
        if (aboveH > 10)
            strips.Add((oX, oY - aboveH, oW, aboveH));

        // Below strip — clamped to work area bottom (NOT screen bottom, to avoid taskbar)
        int belowY = oY + oH;
        int belowH = Math.Min(StripSize, screenBottom - belowY);
        if (belowH > 10)
            strips.Add((oX, belowY, oW, belowH));

        var stripBrightnesses = new List<double>();

        foreach (var (sx, sy, sw, sh) in strips)
        {
            if (sw <= 5 || sh <= 5) continue; // Skip degenerate strips

            var hdcMem = Win32Api.CreateCompatibleDC(hdcScreen);
            var hBmp = Win32Api.CreateCompatibleBitmap(hdcScreen, sw, sh);
            var hOldBmp = Win32Api.SelectObject(hdcMem, hBmp);

            Win32Api.BitBlt(hdcMem, 0, 0, sw, sh, hdcScreen, sx, sy, Win32Api.SRCCOPY);

            double stripB = AnalyzeRegionFromDC(hdcMem, hBmp, 0, 0, sw, sh);
            if (stripB >= 0)
                stripBrightnesses.Add(stripB);

            Win32Api.SelectObject(hdcMem, hOldBmp);
            Win32Api.DeleteObject(hBmp);
            Win32Api.DeleteDC(hdcMem);
        }

        Win32Api.ReleaseDC(IntPtr.Zero, hdcScreen);

        if (stripBrightnesses.Count == 0)
            return (0.5, 0);

        // Calculate average
        double avg = 0;
        foreach (var b in stripBrightnesses) avg += b;
        avg /= stripBrightnesses.Count;

        // Calculate variance — high variance = mixed content (text, split background)
        double variance = 0;
        foreach (var b in stripBrightnesses)
            variance += (b - avg) * (b - avg);
        variance /= stripBrightnesses.Count;

        return (avg, variance);
    }

    /// <summary>
    /// Gets the work area of the primary monitor (screen minus taskbar).
    /// </summary>
    private static void GetWorkArea(out int left, out int top, out int right, out int bottom)
    {
        try
        {
            // SystemParametersInfo SPI_GETWORKAREA returns the desktop work area
            if (SystemParametersInfo(0x0030 /* SPI_GETWORKAREA */, 0, out Win32Api.RECT rect, 0))
            {
                left = rect.Left;
                top = rect.Top;
                right = rect.Right;
                bottom = rect.Bottom;
                return;
            }
        }
        catch { }

        // Fallback: use full screen from GetSystemMetrics
        left = 0;
        top = 0;
        right = GetSystemMetrics(0 /* SM_CXSCREEN */);
        bottom = GetSystemMetrics(1 /* SM_CYSCREEN */);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam,
        out Win32Api.RECT pvParam, uint fWinIni);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    /// <summary>
    /// Fast pixel analysis using GetDIBits. Returns average perceived brightness (0..1).
    /// Returns -1 on failure. Samples every 8th pixel.
    /// </summary>
    private static double AnalyzeRegionFromDC(IntPtr hdcMem, IntPtr hBitmap,
        int regionX, int regionY, int regionW, int regionH)
    {
        if (regionW <= 0 || regionH <= 0) return -1;

        var bmi = new Win32Api.BITMAPINFO();
        bmi.bmiHeader.biSize = (uint)Marshal.SizeOf<Win32Api.BITMAPINFOHEADER>();

        Win32Api.GetDIBits(hdcMem, hBitmap, 0, 0, IntPtr.Zero, ref bmi, Win32Api.DIB_RGB_COLORS);

        int fullW = Math.Abs(bmi.bmiHeader.biWidth);
        int fullH = Math.Abs(bmi.bmiHeader.biHeight);
        if (fullW <= 0 || fullH <= 0) return -1;

        bmi.bmiHeader.biWidth = fullW;
        bmi.bmiHeader.biHeight = -fullH;
        bmi.bmiHeader.biPlanes = 1;
        bmi.bmiHeader.biBitCount = 32;
        bmi.bmiHeader.biCompression = 0;

        int bufferSize = fullW * fullH * 4;
        var buffer = Marshal.AllocHGlobal(bufferSize);

        try
        {
            int result = Win32Api.GetDIBits(hdcMem, hBitmap, 0, (uint)fullH,
                buffer, ref bmi, Win32Api.DIB_RGB_COLORS);
            if (result <= 0) return -1;

            double totalLum = 0;
            int sampleCount = 0;
            int step = 8; // Sample every 8th pixel for speed

            unsafe
            {
                byte* ptr = (byte*)buffer.ToPointer();

                for (int py = regionY; py < regionY + regionH && py < fullH; py += step)
                {
                    int rowOffset = py * fullW * 4;
                    for (int px = regionX; px < regionX + regionW && px < fullW; px += step)
                    {
                        int offset = rowOffset + px * 4;
                        byte b = ptr[offset];
                        byte g = ptr[offset + 1];
                        byte r = ptr[offset + 2];

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
    /// Applies hysteresis: only flips if brightness crosses the far threshold.
    /// Dead zone 0.40–0.60 prevents rapid toggling for borderline backgrounds.
    /// </summary>
    private static bool ApplyHysteresis(double brightness)
    {
        if (_lastDecisionIsLight == null)
        {
            _lastDecisionIsLight = brightness >= 0.50;
            AdaptiveDebugLog.Log($"Hysteresis: INITIAL brightness={brightness:F3} → isLight={_lastDecisionIsLight}");
            return _lastDecisionIsLight.Value;
        }

        bool oldDecision = _lastDecisionIsLight.Value;

        if (_lastDecisionIsLight.Value)
        {
            if (brightness < LowerThreshold)
                _lastDecisionIsLight = false;
        }
        else
        {
            if (brightness > UpperThreshold)
                _lastDecisionIsLight = true;
        }

        if (oldDecision != _lastDecisionIsLight.Value)
            AdaptiveDebugLog.Log($"Hysteresis: CHANGED brightness={brightness:F3} → isLight={_lastDecisionIsLight}");

        return _lastDecisionIsLight.Value;
    }

    public static void ResetState() => _lastDecisionIsLight = null;
}
