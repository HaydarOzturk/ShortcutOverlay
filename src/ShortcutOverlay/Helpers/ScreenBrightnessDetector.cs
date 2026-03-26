using System.Runtime.InteropServices;
using ShortcutOverlay.NativeInterop;

namespace ShortcutOverlay.Helpers;

/// <summary>
/// Ultra-fast screen brightness detector using DIB buffer scanning (not GetPixel).
/// Samples thin strips around the overlay to avoid reading its own pixels.
/// Uses hysteresis to prevent rapid toggling near the threshold.
/// </summary>
public static class ScreenBrightnessDetector
{
    private const double UpperThreshold = 0.58;
    private const double LowerThreshold = 0.42;
    private const int StripWidth = 30;

    private static bool? _lastDecisionIsLight;

    /// <summary>
    /// Samples strips around the overlay and returns average brightness 0..1.
    /// Uses GetDIBits for ~400x faster pixel access than GetPixel.
    /// </summary>
    public static double GetAverageBrightness(int overlayX, int overlayY, int overlayW, int overlayH)
    {
        if (overlayW <= 0 || overlayH <= 0) return 0.5;

        var hdcScreen = Win32Api.GetDC(IntPtr.Zero);
        if (hdcScreen == IntPtr.Zero) return 0.5;

        // 4 strips around the overlay
        var strips = new (int x, int y, int w, int h)[]
        {
            (Math.Max(0, overlayX - StripWidth), overlayY, StripWidth, overlayH),
            (overlayX + overlayW, overlayY, StripWidth, overlayH),
            (overlayX, Math.Max(0, overlayY - StripWidth), overlayW, StripWidth),
            (overlayX, overlayY + overlayH, overlayW, StripWidth),
        };

        double totalLuminance = 0;
        long totalSamples = 0;

        foreach (var (sx, sy, sw, sh) in strips)
        {
            if (sw <= 0 || sh <= 0) continue;

            SampleRegionFast(hdcScreen, sx, sy, sw, sh, ref totalLuminance, ref totalSamples);
        }

        Win32Api.ReleaseDC(IntPtr.Zero, hdcScreen);
        return totalSamples > 0 ? totalLuminance / totalSamples : 0.5;
    }

    /// <summary>
    /// Captures a screen region into a DIB and scans the pixel buffer directly.
    /// Every 8th pixel is sampled for speed.
    /// </summary>
    private static void SampleRegionFast(IntPtr hdcScreen, int x, int y, int w, int h,
        ref double totalLuminance, ref long totalSamples)
    {
        var hdcMem = Win32Api.CreateCompatibleDC(hdcScreen);
        var hBitmap = Win32Api.CreateCompatibleBitmap(hdcScreen, w, h);
        var hOld = Win32Api.SelectObject(hdcMem, hBitmap);

        Win32Api.BitBlt(hdcMem, 0, 0, w, h, hdcScreen, x, y, Win32Api.SRCCOPY);

        // Set up BITMAPINFO for 32bpp top-down DIB
        var bmi = new Win32Api.BITMAPINFO();
        bmi.bmiHeader.biSize = (uint)Marshal.SizeOf<Win32Api.BITMAPINFOHEADER>();
        bmi.bmiHeader.biWidth = w;
        bmi.bmiHeader.biHeight = -h; // Negative = top-down (no row reversal needed)
        bmi.bmiHeader.biPlanes = 1;
        bmi.bmiHeader.biBitCount = 32;
        bmi.bmiHeader.biCompression = 0; // BI_RGB

        int bufferSize = w * h * 4;
        var buffer = Marshal.AllocHGlobal(bufferSize);

        try
        {
            int result = Win32Api.GetDIBits(hdcMem, hBitmap, 0, (uint)h, buffer, ref bmi, Win32Api.DIB_RGB_COLORS);
            if (result > 0)
            {
                // Scan every 8th pixel for speed
                int step = 8;
                unsafe
                {
                    byte* ptr = (byte*)buffer.ToPointer();
                    for (int py = 0; py < h; py += step)
                    {
                        int rowOffset = py * w * 4;
                        for (int px = 0; px < w; px += step)
                        {
                            int offset = rowOffset + px * 4;
                            // DIB format: B G R A
                            byte b = ptr[offset];
                            byte g = ptr[offset + 1];
                            byte r = ptr[offset + 2];

                            // ITU-R BT.709 perceived luminance
                            double lum = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255.0;
                            totalLuminance += lum;
                            totalSamples++;
                        }
                    }
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
            Win32Api.SelectObject(hdcMem, hOld);
            Win32Api.DeleteObject(hBitmap);
            Win32Api.DeleteDC(hdcMem);
        }
    }

    public static bool IsBackgroundLight(int overlayX, int overlayY, int overlayW, int overlayH)
    {
        double brightness = GetAverageBrightness(overlayX, overlayY, overlayW, overlayH);

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
