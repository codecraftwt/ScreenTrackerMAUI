using Blazorise;
using Microsoft.JSInterop;
using Microsoft.Maui.ApplicationModel;
using ScreenTracker1.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;


namespace ScreenTracker1.Services
{
    public class AppUsageTracker : IActiveWindowTrackerService
    {
        private readonly HttpClient _httpClient;
        private readonly IJSRuntime _jsRuntime;
        private System.Timers.Timer _timer;
        private TrackedApp _currentApp;
        private AppTitleModel _currentTitleSession;
        private readonly SemaphoreSlim _trackingGate = new(1, 1);
        private string _startMode = "automatic";
        private int _userId;


        public static readonly Dictionary<string, string> _friendlyAppNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ApplicationFrameHost", "Windows App" },
            { "msedgewebview2", "Microsoft Edge" },
            //{ "ShellExperienceHost", "Windows Shell" },
            { "ShellExperienceHost", "System Interface (Start Menu)" },
            { "SearchHost", "Windows Search" },
            { "LockApp", "Windows Lock Screen" },
            { "chrome", "Google Chrome" },
            { "notepad", "Notepad" },
            { "notepad++", "Notepad++" },
            { "Slack", "Slack" },
             { "code", "Visual Studio Code" },
            { "pgAdmin4", "pgAdmin 4" },
            { "explorer", "File Explorer" },
            { "devenv", "Visual Studio" },
            { "msedge", "Microsoft Edge" },
            { "chrome_proxy", "Google Chrome" },
            { "Microsoft.Photos", "Microsoft Photos" },
            { "Discord", "Discord" },
            { "ms-teams", "Microsoft Teams" },
            { "calc", "Calculator" },
             { "ShellHost", "Windows Shell" },
             { "SystemSettings", "Settings" },
        };

        public List<TrackedApp> AppUsageLogs { get; private set; } = new();
        public List<AppTitleModel> TitleLogs { get; private set; } = new();
        public AppTitleModel? CurrentTitleSession => _currentTitleSession;
        public bool IsRunning => _timer != null;
        public event Action OnAppListChanged;

        public static List<string> DebugPostResults { get; } = new();

        public AppUsageTracker(HttpClient httpClient, IJSRuntime jsRuntime)
        {
            _httpClient = httpClient;
            _jsRuntime = jsRuntime;
        }

        public void Start(string startMode)
        {
            _startMode = startMode.ToLower();
            if (_timer == null)
            {
#if MACCATALYST
                // Native frontmost-app detection is inexpensive. A one-second
                // sample prevents normal, short Mac app switches being skipped.
                _timer = new System.Timers.Timer(1000);
#else
                _timer = new System.Timers.Timer(3000);
#endif
                _timer.Elapsed += async (s, e) => await TrackActiveApp();
                _timer.Start();
                _ = TrackActiveApp();
            }
        }

        public void StartAutomaticTracking()
        {
            _startMode = "automatic";
            Start(_startMode);
        }

        private async Task TrackActiveApp()
        {
            await _trackingGate.WaitAsync();
            try
            {
                await TrackActiveAppCore();
            }
            finally
            {
                _trackingGate.Release();
            }
        }

        private async Task TrackActiveAppCore()
        {
#if WINDOWS
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;

            var buffer = new StringBuilder(256);
            GetWindowText(hwnd, buffer, 256);
            string windowTitle = buffer.ToString();

            GetWindowThreadProcessId(hwnd, out int pid);
            string appName = GetFriendlyAppNameFromProcess(pid, windowTitle);

            if (string.IsNullOrEmpty(appName) || appName == "Unknown")
            {
                if (_currentApp != null)
                {
                    _currentApp.EndTime = DateTime.UtcNow;
                    var usage = new AppUsageModel
                    {
                        AppName = _currentApp.AppName,
                        StartTime = _currentApp.StartTime,
                        EndTime = _currentApp.EndTime,
                        StartMode = _startMode
                    };
                    _ = SendToApi("AppUsage", usage);
                    _currentApp = null;
                }
                EndCurrentTitleSession();
                return;
            }
#elif MACCATALYST
            (string appName, string windowTitle) = DetectMacActiveWindow();
            if (string.IsNullOrEmpty(appName) || appName == "Unknown")
            {
                if (_currentApp != null)
                {
                    _currentApp.EndTime = DateTime.UtcNow;
                    var usage = new AppUsageModel
                    {
                        AppName = _currentApp.AppName,
                        StartTime = _currentApp.StartTime,
                        EndTime = _currentApp.EndTime,
                        StartMode = _startMode
                    };
                    _ = SendToApi("AppUsage", usage);
                    _currentApp = null;
                }
                EndCurrentTitleSession();
                return;
            }
#else
            return;
#endif

            var now = DateTime.UtcNow;

            if (_currentApp == null || _currentApp.AppName != appName)
            {
                if (_currentApp != null)
                {
                    _currentApp.EndTime = DateTime.UtcNow;

                    var usage = new AppUsageModel
                    {
                        AppName = _currentApp.AppName,
                        StartTime = _currentApp.StartTime,
                        EndTime = _currentApp.EndTime,
                        StartMode = _startMode
                    };

                    _ = SendToApi("AppUsage", usage);
                }

                _currentApp = new TrackedApp
                {
                    AppName = appName,
                    StartTime = now,
                    EndTime = now,
                    StartMode = _startMode
                };

                if (MainThread.IsMainThread)
                {
                    OnAppListChanged?.Invoke();
                }
                else
                {
                    await MainThread.InvokeOnMainThreadAsync(() => OnAppListChanged?.Invoke());
                }
            }

            if (_currentTitleSession == null ||
                _currentTitleSession.Title != windowTitle ||
                _currentTitleSession.AppName != appName)
            {
                EndCurrentTitleSession();

                _currentTitleSession = new AppTitleModel
                {
                    AppName = appName,
                    Title = windowTitle,
                    StartTime = DateTime.UtcNow
                };
            }
        }

        public Task CaptureActiveAppNowAsync()
        {
            return TrackActiveApp();
        }

        private async Task SendToApi<T>(string endpoint, T model)
        {
            try
            {
                var token = Preferences.Get("authToken", null);

                if (!string.IsNullOrEmpty(token))
                {
                    var json = JsonSerializer.Serialize(model);
                    var request = new HttpRequestMessage(HttpMethod.Post, $"{App.URL}{endpoint}");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.SendAsync(request);
                    string respBody = await response.Content.ReadAsStringAsync();
                    string result = $"{endpoint} -> {(int)response.StatusCode} | {respBody.Substring(0, Math.Min(respBody.Length, 100))}";
                    DebugPostResults.Insert(0, result);
                    if (DebugPostResults.Count > 10) DebugPostResults.RemoveAt(10);

                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Post to {endpoint} FAILED ({(int)response.StatusCode}): {respBody}");
                    }
                }
                else
                {
                    Console.WriteLine($"[SendToApi] No auth token for {endpoint}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending to {endpoint}: {ex.Message}");
            }
        }

        private async void EndCurrentTitleSession()
        {
            var completedSession = _currentTitleSession;
            if (completedSession == null)
                return;

            // Clear the shared session before awaiting the API. Start/Stop can
            // otherwise reuse it and post overlapping time ranges.
            _currentTitleSession = null;
            completedSession.EndTime = DateTime.UtcNow;
            await SendToApi("AppTitle", completedSession);
        }

        public void Stop()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
                _timer = null;
                EndCurrentTitleSession();

                if (_currentApp != null)
                {
                    var completedApp = _currentApp;
                    _currentApp = null;
                    completedApp.EndTime = DateTime.UtcNow;

                    var usage = new AppUsageModel
                    {
                        AppName = completedApp.AppName,
                        StartTime = completedApp.StartTime,
                        EndTime = completedApp.EndTime,
                        StartMode = _startMode
                    };
                    _ = SendToApi("AppUsage", usage);
                }
            }
        }

        public void SetUserId(int userId)
        {
            _userId = userId;
            Console.WriteLine($"[AppUsageTracker] SetUserId({userId})");
        }

        public void ClearUserData()
        {
            TitleLogs.Clear();
            AppUsageLogs.Clear();
        }

        private string GetFriendlyAppNameFromProcess(int pid, string windowTitle)
        {
            string appName = "Unknown";
            try
            {
                var proc = Process.GetProcessById(pid);
                string processName = proc.ProcessName;
                if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    processName = processName.Substring(0, processName.Length - 4);
                }

                if (_friendlyAppNames.TryGetValue(processName, out string friendlyName))
                {
                    if (processName.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
                    {
                        appName = !string.IsNullOrWhiteSpace(windowTitle) ? windowTitle : friendlyName;
                    }
                    else
                    {
                        appName = friendlyName;
                    }
                }
                else
                {
                    appName = processName;
                }
            }
            catch { }
            return appName;
        }

#if WINDOWS
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);
#endif

#if MACCATALYST
        private static (string appName, string windowTitle) DetectMacActiveWindow()
        {
            var (name, title) = ScreenTracker1.Platforms.MacCatalyst.MacActiveWindowNative.GetActiveWindowInfo();
            name = FilterMacAppName(name ?? string.Empty);
            bool hasNativeApp = !string.IsNullOrEmpty(name) && name != "Unknown";
            if (hasNativeApp && !string.IsNullOrWhiteSpace(title))
            {
                Console.WriteLine($"[MacAppDetection] CG native: app={name}, title={title}");
                return (name, title ?? string.Empty);
            }

            Console.WriteLine(hasNativeApp
                ? $"[MacAppDetection] CG title unavailable for {name}, trying Accessibility fallback..."
                : "[MacAppDetection] CG native failed, trying osascript fallback...");

            try
            {
                var ps = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/usr/bin/osascript",
                        Arguments = "-e \"tell application \\\"System Events\\\" to get {name of first process whose frontmost is true, name of first window of (first process whose frontmost is true)}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                ps.Start();
                string? output = ps.StandardOutput.ReadToEnd()?.Trim();
                ps.WaitForExit(2000);

                if (!string.IsNullOrEmpty(output) && !output.Contains("error"))
                {
                    string[] parts = output.Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    string appName = parts.Length > 0 ? parts[0].Trim() : "Unknown";
                    string windowTitle = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                    appName = FilterMacAppName(appName);
                    if (!string.IsNullOrWhiteSpace(windowTitle))
                        return (appName, windowTitle);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MacAppDetection] osascript error: {ex.Message}");
            }

            // Keep the reliably detected native application even when macOS privacy
            // prevents both APIs from exposing its focused window title.
            if (hasNativeApp)
                return (name, string.Empty);

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
                {
                    string appName = FilterMacAppName(output);
                    return (appName, string.Empty);
                }
            }
            catch { }

            return ("Unknown", string.Empty);
        }

        private static string FilterMacAppName(string appName)
        {
            return appName;
        }
#endif
    }
}
