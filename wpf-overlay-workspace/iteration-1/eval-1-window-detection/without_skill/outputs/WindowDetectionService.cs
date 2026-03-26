using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace ShortcutOverlay.Services
{
    /// <summary>
    /// Service for detecting which application is currently in the foreground.
    /// Handles Desktop, File Explorer, UWP apps, and Windows Terminal specially.
    /// </summary>
    public class WindowDetectionService
    {
        #region P/Invoke Declarations

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        private const uint GW_OWNER = 4;
        private const uint GW_CHILD = 5;

        #endregion

        #region Models

        /// <summary>
        /// Represents detected foreground application information
        /// </summary>
        public class ForegroundWindowInfo
        {
            public IntPtr WindowHandle { get; set; }
            public string WindowTitle { get; set; }
            public string ProcessName { get; set; }
            public string ClassName { get; set; }
            public uint ProcessId { get; set; }
            public ApplicationType AppType { get; set; }
            public string FilePath { get; set; }
        }

        /// <summary>
        /// Types of applications that may require special handling
        /// </summary>
        public enum ApplicationType
        {
            Unknown,
            Desktop,
            FileExplorer,
            UwpApp,
            WindowsTerminal,
            StandardApplication
        }

        #endregion

        /// <summary>
        /// Gets information about the currently foreground window
        /// </summary>
        public ForegroundWindowInfo GetForegroundWindowInfo()
        {
            IntPtr foregroundWindowHandle = GetForegroundWindow();

            if (foregroundWindowHandle == IntPtr.Zero)
            {
                return null;
            }

            var windowInfo = new ForegroundWindowInfo
            {
                WindowHandle = foregroundWindowHandle
            };

            // Get window title
            windowInfo.WindowTitle = GetWindowTextFromHandle(foregroundWindowHandle);

            // Get class name
            windowInfo.ClassName = GetClassNameFromHandle(foregroundWindowHandle);

            // Get process information
            GetWindowThreadProcessId(foregroundWindowHandle, out uint processId);
            windowInfo.ProcessId = processId;

            try
            {
                var process = Process.GetProcessById((int)processId);
                windowInfo.ProcessName = process.ProcessName;
                windowInfo.FilePath = process.MainModule?.FileName ?? string.Empty;
                process.Dispose();
            }
            catch
            {
                // Process may have terminated or access denied
                windowInfo.ProcessName = "Unknown";
                windowInfo.FilePath = string.Empty;
            }

            // Determine application type
            windowInfo.AppType = DetermineApplicationType(windowInfo);

            return windowInfo;
        }

        /// <summary>
        /// Determines the type of application based on window characteristics
        /// </summary>
        private ApplicationType DetermineApplicationType(ForegroundWindowInfo windowInfo)
        {
            // Check for Desktop
            if (IsDesktopWindow(windowInfo))
            {
                return ApplicationType.Desktop;
            }

            // Check for Windows Terminal
            if (IsWindowsTerminal(windowInfo))
            {
                return ApplicationType.WindowsTerminal;
            }

            // Check for File Explorer
            if (IsFileExplorer(windowInfo))
            {
                return ApplicationType.FileExplorer;
            }

            // Check for UWP App
            if (IsUwpApp(windowInfo))
            {
                return ApplicationType.UwpApp;
            }

            return ApplicationType.StandardApplication;
        }

        /// <summary>
        /// Checks if the window is the Desktop
        /// </summary>
        private bool IsDesktopWindow(ForegroundWindowInfo windowInfo)
        {
            // Desktop window typically has class "WorkerW" or "Progman"
            if (windowInfo.ClassName == "WorkerW" || windowInfo.ClassName == "Progman")
            {
                return true;
            }

            // Also check if it's the shell window
            IntPtr shellWindow = GetShellWindow();
            return windowInfo.WindowHandle == shellWindow;
        }

        /// <summary>
        /// Checks if the window is Windows Terminal
        /// </summary>
        private bool IsWindowsTerminal(ForegroundWindowInfo windowInfo)
        {
            // Windows Terminal process name or class
            if (windowInfo.ProcessName == "WindowsTerminal" ||
                windowInfo.ProcessName == "wt" ||
                windowInfo.ClassName.Contains("CASCADIA_HOSTING_WINDOW_CLASS") ||
                windowInfo.ClassName == "VirtualConsoleClass")
            {
                return true;
            }

            // Check window title as fallback
            if (windowInfo.WindowTitle != null &&
                (windowInfo.WindowTitle.Contains("Windows Terminal") ||
                 windowInfo.WindowTitle.StartsWith("Administrator:")))
            {
                return IsWindowsTerminalByClassName(windowInfo.WindowHandle);
            }

            return false;
        }

        /// <summary>
        /// Helper to verify Windows Terminal by checking class hierarchy
        /// </summary>
        private bool IsWindowsTerminalByClassName(IntPtr hWnd)
        {
            try
            {
                string className = GetClassNameFromHandle(hWnd);
                return className.Contains("CASCADIA") || className == "VirtualConsoleClass";
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if the window is File Explorer
        /// </summary>
        private bool IsFileExplorer(ForegroundWindowInfo windowInfo)
        {
            if (windowInfo.ProcessName == "explorer")
            {
                // Make sure it's not the desktop - explorer.exe runs both
                if (windowInfo.ClassName != "WorkerW" && windowInfo.ClassName != "Progman")
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if the window is a UWP (Universal Windows Platform) application
        /// </summary>
        private bool IsUwpApp(ForegroundWindowInfo windowInfo)
        {
            // UWP apps typically have "ApplicationFrameWindow" as parent class
            try
            {
                if (windowInfo.ClassName == "ApplicationFrameWindow")
                {
                    return true;
                }

                // Check for CoreWindow which is used by some UWP apps
                if (windowInfo.ClassName == "CoreWindow")
                {
                    return true;
                }

                // UWP apps typically have process names like "CalculatorApp", "Photos", "Mail", etc.
                // and are located in Program Files\WindowsApps
                if (windowInfo.FilePath != null &&
                    windowInfo.FilePath.Contains("WindowsApps"))
                {
                    return true;
                }

                // Try to get the parent window class
                IntPtr parentWindow = GetWindow(windowInfo.WindowHandle, GW_OWNER);
                if (parentWindow != IntPtr.Zero && parentWindow != windowInfo.WindowHandle)
                {
                    string parentClassName = GetClassNameFromHandle(parentWindow);
                    if (parentClassName == "ApplicationFrameWindow")
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // If we can't determine, assume it's not a UWP app
            }

            return false;
        }

        /// <summary>
        /// Gets the window text/title from a window handle
        /// </summary>
        private string GetWindowTextFromHandle(IntPtr hWnd)
        {
            try
            {
                int length = GetWindowTextLength(hWnd);
                if (length <= 0)
                {
                    return string.Empty;
                }

                StringBuilder sb = new StringBuilder(length + 1);
                GetWindowText(hWnd, sb, length + 1);
                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets the class name from a window handle
        /// </summary>
        private string GetClassNameFromHandle(IntPtr hWnd)
        {
            try
            {
                StringBuilder sb = new StringBuilder(256);
                GetClassName(hWnd, sb, sb.Capacity);
                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Monitors for changes to the foreground window (for event-based detection)
        /// </summary>
        public class WindowChangeMonitor
        {
            private IntPtr _lastForegroundWindow;
            private Action<ForegroundWindowInfo> _onWindowChanged;
            private readonly WindowDetectionService _detectionService;

            public WindowChangeMonitor(WindowDetectionService detectionService,
                Action<ForegroundWindowInfo> onWindowChanged)
            {
                _detectionService = detectionService ?? throw new ArgumentNullException(nameof(detectionService));
                _onWindowChanged = onWindowChanged ?? throw new ArgumentNullException(nameof(onWindowChanged));
                _lastForegroundWindow = GetForegroundWindow();
            }

            /// <summary>
            /// Checks if the foreground window has changed (call this periodically)
            /// </summary>
            public void CheckForWindowChange()
            {
                IntPtr currentForegroundWindow = GetForegroundWindow();

                if (currentForegroundWindow != _lastForegroundWindow)
                {
                    _lastForegroundWindow = currentForegroundWindow;
                    var windowInfo = _detectionService.GetForegroundWindowInfo();
                    _onWindowChanged?.Invoke(windowInfo);
                }
            }
        }
    }
}
