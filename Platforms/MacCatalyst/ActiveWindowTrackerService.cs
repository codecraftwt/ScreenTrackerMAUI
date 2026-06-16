using System;
using System.Diagnostics;
using Foundation;
using AppKit;

namespace ScreenTracker1.Platforms.MacCatalyst
{
    /// <summary>
    /// Tracks the active (frontmost) application on macOS using NSWorkspace.
    /// Replacement for the Win32 GetForegroundWindow/GetWindowText approach.
    /// </summary>
    public class ActiveWindowTrackerService : IDisposable
    {
        private NSObject? _activationObserver;
        private NSObject? _spaceObserver;

        /// <summary>
        /// Fires when the frontmost application or its active window title changes.
        /// Parameters: (appName, windowTitle)
        /// </summary>
        public event Action<string, string>? OnActiveWindowChanged;

        public void Start()
        {
            var ws = NSWorkspace.SharedWorkspace;

            // 1. Observe when the frontmost application changes
            _activationObserver = NSNotificationCenter.DefaultCenter.AddObserver(
                NSWorkspace.DidActivateApplicationNotification,
                notification =>
                {
                    var app = ws.FrontmostApplication;
                    if (app != null)
                    {
                        string appName = app.LocalizedName ?? app.BundleIdentifier ?? "Unknown";
                        string windowTitle = GetMainWindowTitle(app);
                        OnActiveWindowChanged?.Invoke(appName, windowTitle);
                    }
                }
            );

            // 2. Observe when the active space (virtual desktop) changes
            _spaceObserver = NSNotificationCenter.DefaultCenter.AddObserver(
                NSWorkspace.ActiveSpaceDidChangeNotification,
                notification =>
                {
                    var app = ws.FrontmostApplication;
                    if (app != null)
                    {
                        string appName = app.LocalizedName ?? app.BundleIdentifier ?? "Unknown";
                        string windowTitle = GetMainWindowTitle(app);
                        OnActiveWindowChanged?.Invoke(appName, windowTitle);
                    }
                }
            );
        }

        /// <summary>
        /// Gets the frontmost application's information immediately.
        /// </summary>
        public (string appName, string windowTitle) GetActiveWindowInfo()
        {
            var app = NSWorkspace.SharedWorkspace.FrontmostApplication;
            if (app == null) return ("Unknown", "");

            string appName = app.LocalizedName ?? app.BundleIdentifier ?? "Unknown";
            string windowTitle = GetMainWindowTitle(app);
            return (appName, windowTitle);
        }

        /// <summary>
        /// Attempts to get the frontmost window title via AppleScript.
        /// Requires Accessibility permission for full window title access.
        /// </summary>
        private static string GetMainWindowTitle(NSRunningApplication app)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/usr/bin/osascript",
                        Arguments = "-e \"tell application \\\"System Events\\\" to get name of first window of (first process whose frontmost is true)\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string? result = process.StandardOutput.ReadToEnd()?.Trim();
                process.WaitForExit(2000);

                if (!string.IsNullOrEmpty(result))
                    return result;
            }
            catch
            {
                // Fallback: use the app's localized name as the title
            }

            return app.LocalizedName ?? "Unknown";
        }

        public void Stop()
        {
            if (_activationObserver != null)
            {
                NSNotificationCenter.DefaultCenter.RemoveObserver(_activationObserver);
                _activationObserver = null;
            }
            if (_spaceObserver != null)
            {
                NSNotificationCenter.DefaultCenter.RemoveObserver(_spaceObserver);
                _spaceObserver = null;
            }
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }
    }
}
