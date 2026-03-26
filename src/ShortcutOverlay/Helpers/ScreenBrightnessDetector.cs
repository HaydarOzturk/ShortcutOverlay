using System.Runtime.InteropServices;
using ShortcutOverlay.NativeInterop;

namespace ShortcutOverlay.Helpers;

/// <summary>
/// Production-quality background brightness detector.
///
/// Strategy: Instead of sampling screen pixels (which includes our own overlay),
/// we use PrintWindow(PW_RENDERFULLCONTENT) to capture the foreground window's
/// actual rendered content, then analyze the region where our overlay sits.
/// This gives us exactly what's behind the overlay without self-sampling.
///
/// Falls back to screen-edge sampling if PrintWindow fails (e.g., fullscreen games).
/// Uses hysteresis to prevent rapid toggling near the threshold.
/// Uses GetDIBits for fast pixel buffer access (~400x faster than GetPixel).
/// </summary>
public static class ScreenBrightnessDetector
{
    // Hysteresis thresholds — wide dead zone prevents flickering
    private const double UpperThreshold = 0.58;
    private const double LowerThreshold = 0.42;

    private static bool? _lastDecisionIsLight;

    /// <summary>
    /// Detects whether the background behind the overlay is light or dark.
    /// Uses the foreground window's HWND to capture its content directly.
    /// </summary>
    /// <param name="foregroundHwnd">HWND of the current foreground window</param>
    /// <param name="overlayScreenX">Overlay left edge in physical screen pixels</param>
    /// <param name="overlayScreenY">Overlay top edge in physical screen pixels</param>
    /// <param name="overlayW">Overlay width in physical pixels</param>
    /// <param name="overlayH">Overlay height in physical pixels</param>
    public static bool IsBackgroundLight(IntPtr foregroundHwnd,
        int overlayScreenX, int overlayScreenY, int overlayW, int overlayH)
    {
        double brightness;

        // Try PrintWindow first (most accurate)
        if (foregroundHwnd != IntPtr.Zero)
        {
            brightness = GetBrightnessViaPrintWindow(foregroundHwnd,
                overlayScreenX, overlayScreenY, overlayW, overlayH);

            if (brightness >= 0) // -1 means PrintWindow failed
                return ApplyHysteresis(brightness);
        }

        // Fallback: sample screen strips around the overlay edges
        brightness = GetBrightnessViaScreenStrips(overlayScreenX, overlayScreenY, overlayW, overlayH);
        return ApplyHysteresis(brightness);
    }

    /// <summary>
    /// Captures the foreground window via PrintWindow(PW_RENDERFULLCONTENT),
    /// maps our overlay's screen position to the window's client area,
    /// and analyzes the overlapping region's brightness.
    /// Returns -1 if PrintWindow fails.
    /// </summary>
    private static double GetBrightnessViaPrintWindow(IntPtr hwnd,
        int overlayScreenX, int overlayScreenY, int overlayW, int overlayH)
    {
        // Get the foreground window's screen rect
        if (!Win32Api.GetWindowRect(hwnd, out var winRect))
            return -1;

        int winW = winRect.Width;
        int winH = winRect.Height;
        if (winW <= 0 || winH <= 0) return -1;

        // Create a memory DC and bitmap to capture into
        var hdcScreen = Win32Api.GetDC(IntPtr.Zero);
        var hdcMem = Win32Api.CreateCompatibleDC(hdcScreen);
        var hBitmap = Win32Api.CreateCompatibleBitmap(hdcScreen, winW, winH);
        var hOld = Win32Api.SelectObject(hdcMem, hBitmap);
        Win32Api.ReleaseDC(IntPtr.Zero, hdcScreen);

        // Capture the foreground window's content
        bool captured = Win32Api.PrintWindow(hwnd, hdcMem, Win32Api.PW_RENDERFULLCONTENT);
        if (!captured)
        {
            // Cleanup and signal failure
            Win32Api.SelectObject(hdcMem, hOld);
            Win32Api.DeleteObject(hBitmap);
            Win32Api.DeleteDC(hdcMem);
            return -1;
        }

        // Map overlay screen coords to window-relative coords
        int relX = overlayScreenX - winRect.Left;
        int relY = overlayScreenY - winRect.Top;

        // Clamp to the window's bounds
        int sampleX = Math.Max(0, relX);
        int sampleY = Math.Max(0, relY);
        int sampleW = Math.Min(overlayW, winW - sampleX);
        int sampleH = Math.Min(overlayH, winH - sampleY);

        if (sampleW <= 0 || sampleH <= 0)
        {
            Win32Api.SelectObject(hdcMem, hOld);
            Win32Api.DeleteObject(hBitmap);
            Win32Api.DeleteDC(hdcMem);
            return -1;
        }

        // Extract and analyze pixels from the captured region
        double brightness = AnalyzeRegionFromDC(hdcMem, hBitmap, sampleX, sampleY, sampleW, sampleH);

        Win32Api.SelectObject(hdcMem, hOld);
        Win32Api.DeleteObject(hBitmap);
        Win32Api.DeleteDC(hdcMem);

        return brightness;
    }

    /// <summary>
    /// Fallback: samples thin strips around overlay edges from the screen.
    /// Less accurate but works when PrintWindow fails.
    /// </summary>
    private static double GetBrightnessViaScreenStrips(
        int overlayX, int overlayY, int overlayW, int overlayH)
    {
        const int stripW = 30;

        var hdcScreen = Win32Api.GetDC(IntPtr.Zero);
        if (hdcScreen == IntPtr.Zero) return 0.5;

        var strips = new (int x, int y, int w, int h)[]
        {
            (Math.Max(0, overlayX - stripW), overlayY, stripW, overlayH),
            (overlayX + overlayW, overlayY, stripW, overlayH),
            (overlayX, Math.Max(0, overlayY - stripW), overlayW, stripW),
            (overlayX, overlayY + overlayH, overlayW, stripW),
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
                // Approximate: treat each strip as one weighted sample
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
    /// Fast pixel analysis using GetDIBits. Reads a sub-region of a bitmap
    /// and returns average perceived brightness (0..1). Returns -1 on failure.
    /// Samples every 10th pixel for speed.
    /// </summary>
    private static double AnalyzeRegionFromDC(IntPtr hdcMem, IntPtr hBitmap,
        int regionX, int regionY, int regionW, int regionH)
    {
        if (regionW <= 0 || regionH <= 0) return -1;

        // We need the full bitmap for GetDIBits, then sample the sub-region
        // Get bitmap dimensions
        var bmi = new Win32Api.BITMAPINFO();
        bmi.bmiHeader.biSize = (uint)Marshal.SizeOf<Win32Api.BITMAPINFOHEADER>();

        // First call to get dimensions
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
            return _lastDecisionIsLight.Value;
        }

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

        return _lastDecisionIsLight.Value;
    }

    public static void ResetState() => _lastDecisionIsLight = null;
}
