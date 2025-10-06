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
    public class AppUsageTracker
    {
        private readonly HttpClient _httpClient;
        private readonly IJSRuntime _jsRuntime;
        private System.Timers.Timer _timer;
        private TrackedApp _currentApp;
        private AppTitleModel _currentTitleSession;
        private string _startMode = "automatic";


        private readonly Dictionary<string, string> _friendlyAppNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "msedgewebview2", "Microsoft Edge" },
            { "ShellExperienceHost", "Windows Shell" },
            { "SearchHost", "Windows Search" },
            { "LockApp", "Windows Lock Screen" },
            { "chrome", "Google Chrome" },
            { "notepad", "Notepad" },
            { "notepad++", "Notepad++" },
            { "Slack", "Slack" },
             { "code", "Slack" },
            { "pgAdmin4", "pgAdmin 4" },
            { "explorer", "File Explorer" },
            { "devenv", "Visual Studio Code" },
            { "msedge", "Microsoft Edge" },
            { "chrome_proxy", "Google Chrome" },
            { "Microsoft.Photos", "Microsoft Photos" },
            { "Discord", "Discord" },
            { "ms-teams", "Microsoft Teams" },
            { "calc", "Calculator" },
             { "ShellHost", "Windows Shell Host" }
        };

        public List<TrackedApp> AppUsageLogs { get; private set; } = new();
        public List<AppTitleModel> TitleLogs { get; private set; } = new();
        public event Action OnAppListChanged;

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
                _timer = new System.Timers.Timer(3000);
                _timer.Elapsed += async (s, e) => await TrackActiveApp();
                _timer.Start();
            }
        }

        public void StartAutomaticTracking()
        {
            _startMode = "automatic";
            Start(_startMode);
        }

        private async Task TrackActiveApp()
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;

            var buffer = new StringBuilder(256);
            GetWindowText(hwnd, buffer, 256);
            string windowTitle = buffer.ToString();

            GetWindowThreadProcessId(hwnd, out int pid);
            string appName = "Unknown";

            try
            {
                var proc = Process.GetProcessById(pid);
        
                string processName = proc.ProcessName;
                if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    processName = processName.Substring(0, processName.Length - 4);
                }

               
                if (_friendlyAppNames.ContainsKey(processName))
                {
                    appName = _friendlyAppNames[processName];
                }
                else
                {
                  
                    appName = processName;
                }
            }
            catch { }

            var now = DateTime.UtcNow;

            if (_currentApp == null || _currentApp.AppName != appName)
            {
                if (_currentApp != null)
                {
                    _currentApp.EndTime = DateTime.UtcNow;
                    AppUsageLogs.Insert(0, _currentApp);

                    var usage = new AppUsageModel
                    {
                        AppName = _currentApp.AppName,
                        StartTime = _currentApp.StartTime,
                        EndTime = _currentApp.EndTime,
                        StartMode = _startMode
                    };

                    await SendToApi("AppUsage", usage);
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

        private async Task SendToApi<T>(string endpoint, T model)
        {
            try
            {
                var token = Preferences.Get("authToken", null);

                if (!string.IsNullOrEmpty(token))
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, $"{App.URL}{endpoint}");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    request.Content = new StringContent(JsonSerializer.Serialize(model), Encoding.UTF8, "application/json");

                    var response = await _httpClient.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Post to {endpoint} failed: {await response.Content.ReadAsStringAsync()}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending to {endpoint}: {ex.Message}");
            }
        }

        private async void EndCurrentTitleSession()
        {
            if (_currentTitleSession != null)
            {
                _currentTitleSession.EndTime = DateTime.UtcNow;
                TitleLogs.Add(_currentTitleSession);
                await SendToApi("AppTitle", _currentTitleSession);
                _currentTitleSession = null;
            }
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
                    _currentApp.EndTime = DateTime.UtcNow;
                    AppUsageLogs.Insert(0, _currentApp);
                }
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);
    }
}