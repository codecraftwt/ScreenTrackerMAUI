using System;
using System.Diagnostics;

namespace ScreenTracker1.Platforms.MacCatalyst
{
    public class ActiveWindowTrackerService : IDisposable
    {
        public event Action<string, string>? OnActiveWindowChanged;

        private System.Timers.Timer? _pollTimer;
        private string _lastAppName = string.Empty;
        private string _lastWindowTitle = string.Empty;

        public void Start()
        {
            // Immediately get the current active window
            var (appName, title) = GetActiveWindowInfo();
            _lastAppName = appName;
            _lastWindowTitle = title;

            // Poll every 1 second for active window changes
            _pollTimer = new System.Timers.Timer(1000);
            _pollTimer.Elapsed += (s, e) => PollActiveWindow();
            _pollTimer.AutoReset = true;
            _pollTimer.Start();
        }

        private void PollActiveWindow()
        {
            var (appName, windowTitle) = GetActiveWindowInfo();

            if (appName != _lastAppName || windowTitle != _lastWindowTitle)
            {
                _lastAppName = appName;
                _lastWindowTitle = windowTitle;
                OnActiveWindowChanged?.Invoke(appName, windowTitle);
            }
        }

        public (string appName, string windowTitle) GetActiveWindowInfo()
        {
            string appName = DetectActiveApp();
            string title = DetectActiveWindowTitle();
            return (appName, title);
        }

        public string GetActiveAppName()
        {
            string name = DetectActiveApp();
            return FilterSelfApp(name);
        }

        public string GetActiveWindowTitle()
        {
            return DetectActiveWindowTitle();
        }

        private string DetectActiveApp()
        {
            try
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/usr/bin/python3",
                        Arguments = "-c \"import AppKit; ws = AppKit.NSWorkspace.sharedWorkspace(); print(ws.frontmostApplication().localizedName())\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                string? output = proc.StandardOutput.ReadToEnd()?.Trim();
                proc.WaitForExit(2000);
                if (!string.IsNullOrEmpty(output) && !output.Contains("Traceback"))
                    return output;
            }
            catch { }

            try
            {
                var ps = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/usr/bin/osascript",
                        Arguments = "-e \"tell application \\\"System Events\\\" to get name of first process whose frontmost is true\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                ps.Start();
                string? name = ps.StandardOutput.ReadToEnd()?.Trim();
                ps.WaitForExit(2000);
                if (!string.IsNullOrEmpty(name) && !name.Contains("error"))
                    return name;
            }
            catch { }

            try
            {
                var ls = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/ps",
                        Arguments = "-eo pid,comm | sort -r | head -5",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                ls.Start();
                string? psOut = ls.StandardOutput.ReadToEnd()?.Trim();
                ls.WaitForExit(2000);

                var lines = psOut?.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines != null)
                {
                    foreach (var line in lines)
                    {
                        var parts = line.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            string name = System.IO.Path.GetFileNameWithoutExtension(parts[1]);
                            if (!string.IsNullOrEmpty(name) &&
                                !name.Contains("screentracker", StringComparison.OrdinalIgnoreCase) &&
                                !name.Contains("launchd") && !name.Contains("kernel_task"))
                            {
                                return name;
                            }
                        }
                    }
                }
            }
            catch { }

            return "Unknown";
        }

        private string DetectActiveWindowTitle()
        {
            try
            {
                var proc = new Process
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
                proc.Start();
                string? result = proc.StandardOutput.ReadToEnd()?.Trim();
                proc.WaitForExit(2000);
                if (!string.IsNullOrEmpty(result) && !result.Contains("error"))
                    return result;
            }
            catch { }
            return string.Empty;
        }

        private static string FilterSelfApp(string appName)
        {
            if (appName.Contains("screentracker", StringComparison.OrdinalIgnoreCase) ||
                appName.Contains("screen tracker", StringComparison.OrdinalIgnoreCase))
                return "Unknown";
            return appName;
        }

        public void Stop()
        {
            if (_pollTimer != null)
            {
                _pollTimer.Stop();
                _pollTimer.Dispose();
                _pollTimer = null;
            }
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }
    }
}
