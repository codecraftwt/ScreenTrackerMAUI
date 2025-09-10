using System;
using Microsoft.JSInterop;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ScreenTracker1.Services
{
    public class AppTitleTrackerService
    {
        private readonly HttpClient _httpClient;
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);

        private TitleModel? _currentSession;
        public List<TitleModel> Logs { get; set; } = new();

        public async void  CheckActiveWindow()
        {
            IntPtr handle = GetForegroundWindow();
            var buffer = new System.Text.StringBuilder(256);
            GetWindowText(handle, buffer, 256);
            string title = buffer.ToString();

            GetWindowThreadProcessId(handle, out int pid);
            var process = Process.GetProcessById(pid);
            string appName = process.ProcessName;

            if (_currentSession == null ||
                _currentSession.Title != title ||
                _currentSession.AppName != appName)
            {
                EndCurrentSession();

                _currentSession = new TitleModel
                {
                    AppName = appName,
                    Title = title,
                    StartTime = DateTime.UtcNow
                };
                await SendAppUsageAsync(_currentSession);

            }
        }


        private async Task SendAppUsageAsync(TitleModel model)
        {
            try
            {
                var token = Preferences.Get("authToken", null);

                if (!string.IsNullOrEmpty(token))
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, $"{App.URL}AppTitle");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    request.Content = new StringContent(JsonSerializer.Serialize(model), Encoding.UTF8, "application/json");

                    var response = await _httpClient.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Post failed: {await response.Content.ReadAsStringAsync()}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending usage: {ex.Message}");
            }
        }

        public void EndCurrentSession()
        {
            if (_currentSession != null)
            {
                _currentSession.EndTime = DateTime.UtcNow;
                Logs.Add(_currentSession);
                _currentSession = null;
            }
        }

        public TitleModel? GetCurrentSession()
        {
            if (_currentSession != null)
            {
                _currentSession.EndTime = DateTime.UtcNow;
            }
            return _currentSession;
        }

        public class TitleModel
        {
            public string AppName { get; set; }
            public string Title { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }

            public TimeSpan Duration => StartTime - EndTime;
        }
    }
}
