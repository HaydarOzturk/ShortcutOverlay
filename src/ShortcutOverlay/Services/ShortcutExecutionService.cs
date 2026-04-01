using System.Linq;
using System.Runtime.InteropServices;
using ShortcutOverlay.NativeInterop;

namespace ShortcutOverlay.Services;

/// <summary>
/// Parses key combo strings (e.g., "Ctrl+Shift+S") and sends them to the
/// foreground application using Win32 SendInput. More reliable than SendKeys
/// because it works with DirectInput-based apps and games.
///
/// Uses AttachThreadInput to steal the foreground lock from Windows,
/// which is required because our overlay process typically doesn't own
/// the foreground and SetForegroundWindow would silently fail.
/// </summary>
public static class ShortcutExecutionService
{
    /// <summary>
    /// Sends a key combination to the specified window asynchronously.
    /// Brings the target window to foreground, verifies focus transferred,
    /// then sends the keys via SendInput. Must be called from the UI thread.
    /// </summary>
    public static async Task<bool> ExecuteAsync(IntPtr targetHwnd, string keyCombo)
    {
        DebugLogger.Log($">>> ExecuteAsync called | targetHwnd=0x{targetHwnd:X} | keyCombo=\"{keyCombo}\"");

        if (targetHwnd == IntPtr.Zero || string.IsNullOrWhiteSpace(keyCombo))
        {
            DebugLogger.Log($"EARLY EXIT: targetHwnd={targetHwnd == IntPtr.Zero}, keyCombo empty={string.IsNullOrWhiteSpace(keyCombo)}");
            return false;
        }

        var keys = ParseKeyCombo(keyCombo);
        DebugLogger.Log($"ParseKeyCombo → {keys.Count} keys: [{string.Join(", ", keys.Select(k => $"0x{k:X2}"))}]");
        if (keys.Count == 0)
        {
            DebugLogger.Log("EARLY EXIT: no keys parsed");
            return false;
        }

        // Attach our thread to the target window's thread to get foreground rights
        var targetThreadId = GetWindowThreadId(targetHwnd);
        var ourThreadId = Win32Api.GetCurrentThreadId();
        DebugLogger.Log($"Thread IDs: ours={ourThreadId}, target={targetThreadId}");

        bool attached = false;
        if (targetThreadId != 0 && targetThreadId != ourThreadId)
        {
            attached = Win32Api.AttachThreadInput(ourThreadId, targetThreadId, true);
            DebugLogger.LogWin32("AttachThreadInput", attached);
        }
        else
        {
            DebugLogger.Log($"SKIP AttachThreadInput: targetThread={targetThreadId}, same={targetThreadId == ourThreadId}");
        }

        try
        {
            // Log current foreground before we start
            var fgBefore = Win32Api.GetForegroundWindow();
            DebugLogger.Log($"Foreground BEFORE focus switch: 0x{fgBefore:X}");

            // Ensure the target window is in a state that can receive focus
            var showResult = Win32Api.ShowWindow(targetHwnd, Win32Api.SW_SHOW);
            DebugLogger.LogWin32("ShowWindow(SW_SHOW)", showResult);

            var bringResult = Win32Api.BringWindowToTop(targetHwnd);
            DebugLogger.LogWin32("BringWindowToTop", bringResult);

            var setFgResult = Win32Api.SetForegroundWindow(targetHwnd);
            DebugLogger.LogWin32("SetForegroundWindow", setFgResult);

            // Yield to the WPF dispatcher so it can process its own deactivation
            // messages. Thread.Sleep blocks the message pump and prevents the
            // focus transition from completing.
            DebugLogger.Log("await Task.Delay(50) — yielding to dispatcher...");
            await Task.Delay(50);

            // Verify that focus actually transferred to the target window.
            var fgAfter = Win32Api.GetForegroundWindow();
            bool focusConfirmed = fgAfter == targetHwnd;
            DebugLogger.Log($"Focus check #1: foreground=0x{fgAfter:X}, target=0x{targetHwnd:X}, match={focusConfirmed}");

            if (!focusConfirmed)
            {
                DebugLogger.Log("Focus FAILED on first attempt, retrying...");
                // Second attempt: re-attach and retry
                if (attached)
                {
                    Win32Api.AttachThreadInput(ourThreadId, targetThreadId, false);
                    attached = Win32Api.AttachThreadInput(ourThreadId, targetThreadId, true);
                    DebugLogger.LogWin32("AttachThreadInput (retry)", attached);
                }
                var retryBring = Win32Api.BringWindowToTop(targetHwnd);
                DebugLogger.LogWin32("BringWindowToTop (retry)", retryBring);
                var retryFg = Win32Api.SetForegroundWindow(targetHwnd);
                DebugLogger.LogWin32("SetForegroundWindow (retry)", retryFg);
                await Task.Delay(80);

                fgAfter = Win32Api.GetForegroundWindow();
                focusConfirmed = fgAfter == targetHwnd;
                DebugLogger.Log($"Focus check #2: foreground=0x{fgAfter:X}, target=0x{targetHwnd:X}, match={focusConfirmed}");
            }

            if (!focusConfirmed)
            {
                DebugLogger.Log("✗✗✗ GIVING UP: could not transfer focus to target window");
                // Don't give up — send the keys anyway. The focus check may be wrong
                // if the target app uses child windows (e.g., Chrome has many HWNDs).
                DebugLogger.Log("Proceeding with SendInput anyway (target may use child windows)");
            }

            // Small extra delay to let the target app fully process focus
            await Task.Delay(30);

            // Build INPUT array: press all modifiers first, then main key, release in reverse
            var inputs = new List<Win32Api.INPUT>();

            // Key down events (modifiers first, then main key)
            foreach (var key in keys)
                inputs.Add(MakeKeyDown(key));

            // Key up events (reverse order — main key first, modifiers last)
            for (int i = keys.Count - 1; i >= 0; i--)
                inputs.Add(MakeKeyUp(keys[i]));

            var inputArray = inputs.ToArray();
            var structSize = Marshal.SizeOf<Win32Api.INPUT>();
            DebugLogger.Log($"Calling SendInput with {inputArray.Length} events, INPUT struct size={structSize} (expected 40 on x64, 28 on x86)");

            var sent = Win32Api.SendInput(
                (uint)inputArray.Length,
                inputArray,
                structSize);

            var fgAtSend = Win32Api.GetForegroundWindow();
            DebugLogger.Log($"SendInput returned {sent}/{inputArray.Length} | foreground at send time: 0x{fgAtSend:X}");

            if (sent != (uint)inputArray.Length)
            {
                var err = Marshal.GetLastWin32Error();
                DebugLogger.Log($"✗ SendInput PARTIAL/FAILED: sent={sent}, expected={inputArray.Length}, lastError={err}");
            }
            else
            {
                DebugLogger.Log($"✓ SendInput succeeded: {sent} events injected");
            }

            return sent == (uint)inputArray.Length;
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"EXCEPTION in ExecuteAsync: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
        finally
        {
            // Always detach thread input
            if (attached)
            {
                Win32Api.AttachThreadInput(ourThreadId, targetThreadId, false);
                DebugLogger.Log("Detached thread input");
            }
            DebugLogger.Log("<<< ExecuteAsync finished");
        }
    }

    private static uint GetWindowThreadId(IntPtr hwnd)
    {
        return Win32Api.GetWindowThreadProcessId(hwnd, out _);
    }

    /// <summary>
    /// Parses a key combo string like "Ctrl+Shift+S" into a list of virtual key codes.
    /// Modifiers are listed first, main key last.
    /// </summary>
    private static List<ushort> ParseKeyCombo(string keyCombo)
    {
        var result = new List<ushort>();
        var parts = keyCombo.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var vk = MapToVirtualKey(part);
            if (vk != 0)
                result.Add(vk);
        }

        return result;
    }

    // Extended keys that require KEYEVENTF_EXTENDEDKEY flag
    private static readonly HashSet<ushort> ExtendedKeys = new()
    {
        0x2D, 0x2E, 0x24, 0x23, 0x21, 0x22, // Insert, Delete, Home, End, PageUp, PageDown
        0x25, 0x26, 0x27, 0x28,               // Arrow keys
        0x91, 0x90, 0x2C,                     // ScrollLock, NumLock, PrintScreen
        0x5B,                                  // Left Windows key
    };

    /// <summary>
    /// Maps a key name string to its virtual key code.
    /// Handles common modifier names and single-character keys.
    /// </summary>
    private static ushort MapToVirtualKey(string keyName)
    {
        return keyName.ToUpperInvariant() switch
        {
            "CTRL" or "CONTROL" => Win32Api.VK_CONTROL,
            "SHIFT" => Win32Api.VK_SHIFT,
            "ALT" => Win32Api.VK_MENU,
            "WIN" or "WINDOWS" or "SUPER" => Win32Api.VK_LWIN,
            "TAB" => 0x09,
            "ENTER" or "RETURN" => 0x0D,
            "ESC" or "ESCAPE" => 0x1B,
            "SPACE" or "SPACEBAR" => 0x20,
            "BACKSPACE" or "BACK" => 0x08,
            "DELETE" or "DEL" => 0x2E,
            "INSERT" or "INS" => 0x2D,
            "HOME" => 0x24,
            "END" => 0x23,
            "PAGEUP" or "PGUP" => 0x21,
            "PAGEDOWN" or "PGDN" => 0x22,
            "UP" => 0x26,
            "DOWN" => 0x28,
            "LEFT" => 0x25,
            "RIGHT" => 0x27,
            "PRINTSCREEN" or "PRTSC" => 0x2C,
            "SCROLLLOCK" => 0x91,
            "PAUSE" or "BREAK" => 0x13,
            "NUMLOCK" => 0x90,
            "CAPSLOCK" => 0x14,
            "F1" => 0x70, "F2" => 0x71, "F3" => 0x72, "F4" => 0x73,
            "F5" => 0x74, "F6" => 0x75, "F7" => 0x76, "F8" => 0x77,
            "F9" => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
            // Single character keys (A-Z, 0-9)
            _ when keyName.Length == 1 => (ushort)(char.ToUpper(keyName[0])),
            _ => 0
        };
    }

    private static Win32Api.INPUT MakeKeyDown(ushort vk)
    {
        uint flags = 0;
        if (ExtendedKeys.Contains(vk))
            flags |= Win32Api.KEYEVENTF_EXTENDEDKEY;

        return new Win32Api.INPUT
        {
            type = Win32Api.INPUT_KEYBOARD,
            u = new Win32Api.INPUTUNION
            {
                ki = new Win32Api.KEYBDINPUT
                {
                    wVk = vk,
                    wScan = (ushort)Win32Api.MapVirtualKey(vk, Win32Api.MAPVK_VK_TO_VSC),
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }

    private static Win32Api.INPUT MakeKeyUp(ushort vk)
    {
        uint flags = Win32Api.KEYEVENTF_KEYUP;
        if (ExtendedKeys.Contains(vk))
            flags |= Win32Api.KEYEVENTF_EXTENDEDKEY;

        return new Win32Api.INPUT
        {
            type = Win32Api.INPUT_KEYBOARD,
            u = new Win32Api.INPUTUNION
            {
                ki = new Win32Api.KEYBDINPUT
                {
                    wVk = vk,
                    wScan = (ushort)Win32Api.MapVirtualKey(vk, Win32Api.MAPVK_VK_TO_VSC),
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }
}
