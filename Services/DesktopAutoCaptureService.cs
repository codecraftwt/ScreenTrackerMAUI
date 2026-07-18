using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using System.IO;
using System.Net.Http;
using System.Timers;
#if WINDOWS
using Gma.System.MouseKeyHook;
#endif
#if MACCATALYST
using AppKit;
using ScreenTracker1.Platforms.MacCatalyst;
#endif

namespace ScreenTracker1.Services
{
    public class DesktopAutoCaptureService : IAutoCaptureService
    {
        private readonly IScreenshotService _screenshotService;
        private readonly HttpClient _httpClient;
        private System.Timers.Timer _oneMinuteTimer;
        private System.Timers.Timer _tenMinuteTimer;

        private int _userId;
        private string token;
        private string _userName;

        private int _currentMinute = 0;
        private int _keyboardClicks = 0;
        private int _mouseClicks = 0;
        private HashSet<int> _activeKeys = new HashSet<int>();

        private Dictionary<int, (int keyboard, int mouse, DateTime timestamp)> _minuteStats =
            new Dictionary<int, (int, int, DateTime)>();

#if WINDOWS
        private IKeyboardMouseEvents _keyboardMouseEvents;
#elif MACCATALYST
        private GlobalInputMonitorService? _inputMonitor;
#endif
        private string _startMode;

        private DateTime _lastActivityTime = DateTime.Now;
        private bool _isAfk = false;
        private const int AfkThresholdMinutes = 5;

        public DesktopAutoCaptureService(IScreenshotService screenshotService)
        {
            _screenshotService = screenshotService;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(App.URL)
            };
        }

        public void Start(string startMode)
        {
            // STOP TIMER IS CALLED HERE. STOP TIMER IMPLEMENTATION IS UPDATED BELOW.
            StopTimer();

            _startMode = startMode;

            _keyboardClicks = 0;
            _mouseClicks = 0;
            _currentMinute = 0;
            _minuteStats.Clear();

#if WINDOWS
            _keyboardMouseEvents = Hook.GlobalEvents();

            _keyboardMouseEvents.KeyDown += (s, e) => {
                if (_activeKeys.Add((int)e.KeyCode))
                {
                    _keyboardClicks++;
                    _lastActivityTime = DateTime.Now;
                }
            };

            _keyboardMouseEvents.KeyUp += (s, e) => {
                _activeKeys.Remove((int)e.KeyCode);
            };

            _keyboardMouseEvents.MouseDown += (s, e) => {
                _mouseClicks++;
                _lastActivityTime = DateTime.Now;
            };
#elif MACCATALYST
            _inputMonitor = new GlobalInputMonitorService();
            _inputMonitor.OnKeyDown += (evt) => {
                ushort keyCode = evt.KeyCode;
                if (_activeKeys.Add(keyCode))
                {
                    _keyboardClicks++;
                    _lastActivityTime = DateTime.Now;
                }
            };
            _inputMonitor.OnMouseDown += (evt) => {
                _mouseClicks++;
                _lastActivityTime = DateTime.Now;
            };
            _inputMonitor.Start();
#endif

            token = Preferences.Get("authToken", null);
            if (token != null)
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

                var userIdClaim = jwtToken?.Claims
                    .FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

                if (int.TryParse(userIdClaim, out int userId))
                {
                    _userId = userId;
                }

                _userName = jwtToken?.Claims
                    .FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value;

                if (string.IsNullOrEmpty(_userName))
                {
                    _userName = jwtToken?.Claims
                                 .FirstOrDefault(c => c.Type == "email" || c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value;
                }

                if (string.IsNullOrEmpty(_userName))
                {
                    _userName = "UnknownUser";
                }
            }
            else
            {
                _userName = "NoTokenUser";
            }


            _oneMinuteTimer = new System.Timers.Timer(TimeSpan.FromMinutes(1).TotalMilliseconds);
            _oneMinuteTimer.Elapsed += OnOneMinuteElapsed;
            _oneMinuteTimer.AutoReset = true;
            _oneMinuteTimer.Enabled = true;

            _tenMinuteTimer = new System.Timers.Timer(TimeSpan.FromMinutes(10).TotalMilliseconds);
            _tenMinuteTimer.Elapsed += async (s, e) => await CaptureAndSendAsync();
            _tenMinuteTimer.AutoReset = true;
            _tenMinuteTimer.Enabled = true;

            Logger.Log($"USER: {_userName}, INFO: Timers ENABLED at {DateTime.Now:HH:mm:ss.fff}. Cycle length: 10 minutes.");
        }

        private void OnOneMinuteElapsed(object sender, ElapsedEventArgs e)
        {

            int indexToLog = _currentMinute;


            _minuteStats[indexToLog] = (_keyboardClicks, _mouseClicks, DateTime.Now);

            Logger.Log($"USER: {_userName}, INFO: 1-Minute Tick. Logging Minute {indexToLog + 1}. Keyboard: {_keyboardClicks}, Mouse: {_mouseClicks}");

            _keyboardClicks = 0;
            _mouseClicks = 0;

            _currentMinute = (_currentMinute + 1) % 10;
        }

        public void StopTimer()
        {

            if (_oneMinuteTimer != null)
            {
                _oneMinuteTimer.Stop();

                _oneMinuteTimer.Elapsed -= OnOneMinuteElapsed;
                _oneMinuteTimer.Dispose();
                _oneMinuteTimer = null;
            }

            if (_tenMinuteTimer != null)
            {
                _tenMinuteTimer.Stop();

                _tenMinuteTimer.Dispose();
                _tenMinuteTimer = null;
            }

#if WINDOWS
            _keyboardMouseEvents?.Dispose();
            _keyboardMouseEvents = null;
#elif MACCATALYST
            _inputMonitor?.Stop();
            _inputMonitor?.Dispose();
            _inputMonitor = null;
#endif

            _activeKeys.Clear();

            Logger.Log($"USER: {_userName}, INFO: Timers and Hooks STOPPED.");
        }

        private string SerializeMinuteData()
        {
            var minuteData = new Dictionary<string, Dictionary<string, object>>();

            for (int i = 0; i < 10; i++)
            {
                if (_minuteStats.TryGetValue(i, out var stats))
                {

                    minuteData[$"Minute{i + 1}"] = new Dictionary<string, object>
                    {
                        { "Keyboard", stats.keyboard },
                        { "Mouse", stats.mouse },

                        { "Timestamp", stats.timestamp.ToString("o") }
                    };
                }
            }
            return JsonSerializer.Serialize(minuteData);
        }


        public async Task CaptureAndSendAsync()
        {
            try
            {
                // Stop BOTH timers to prevent the 1-minute timer from interrupting 
                // the long-running capture/upload process.
                _oneMinuteTimer.Stop();
                _tenMinuteTimer.Stop();
                Logger.Log($"USER: {_userName}, INFO: 10-Minute Capture started. Timers STOPPED at {DateTime.Now:HH:mm:ss.fff}.");

                // === ROBUSTNESS FIX: Force logging of the currently accumulating minute's data ===
                // This handles cases where the 10-minute timer fires before the 1-minute timer's 10th tick
                // OR if the function is called manually mid-cycle.
                if (_currentMinute > 0)
                {
                    // _currentMinute holds the index of the minute currently running (e.g., if we are in minute 10, index is 9).
                    int indexToCapture = _currentMinute;

                    // Capture the activity accumulated in the counters for this currently running minute
                    _minuteStats[indexToCapture] = (_keyboardClicks, _mouseClicks, DateTime.Now);

                    Logger.Log($"USER: {_userName}, INFO: FORCED Log (Capture). Logging Minute {indexToCapture + 1}. Keyboard: {_keyboardClicks}, Mouse: {_mouseClicks}. (Overrode race condition/interrupt)");

                    // Reset state for the next cycle
                    _keyboardClicks = 0;
                    _mouseClicks = 0;
                    _currentMinute = 0;
                }
                // ==============================================================================


                // 1. Capture Screenshot
                var imageBytes = _screenshotService.CaptureDesktop();

                // 2. Prepare Payload
                var content = new MultipartFormDataContent();
                var byteContent = new ByteArrayContent(imageBytes);
                byteContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");

                // File content
                content.Add(byteContent, "file", $"desktop_screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                // User ID
                content.Add(new StringContent(_userId.ToString()), "userId");

                // Activity Data 
                int totalKeyboard = _minuteStats.Values.Sum(m => m.keyboard);
                int totalMouse = _minuteStats.Values.Sum(m => m.mouse);
                string minuteActivityJson = SerializeMinuteData();

                content.Add(new StringContent(totalKeyboard.ToString()), "keyboardClicks");
                content.Add(new StringContent(totalMouse.ToString()), "mouseClicks");
                content.Add(new StringContent(minuteActivityJson), "minuteActivityData");
                content.Add(new StringContent(_startMode), "startMode");

                // 3. Send Request
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await _httpClient.PostAsync("Image/upload", content);
                response.EnsureSuccessStatusCode();

                // Logging
                Logger.Log($"USER: {_userName}, SUCCESS: Image captured and data sent. Bytes: {imageBytes.Length}, Status: {response.StatusCode}");

                // 4. Reset for the next 10-minute cycle 
                _minuteStats.Clear();

                // Restart BOTH timers, perfectly aligning the next 1-minute and 10-minute cycles from this moment.
                _oneMinuteTimer.Start();
                _tenMinuteTimer.Start();
                Logger.Log($"USER: {_userName}, INFO: Timers RE-ENABLED after capture/upload at {DateTime.Now:HH:mm:ss.fff}.");
            }
            catch (Exception ex)
            {
                // Logging
                Logger.Log($"USER: {_userName}, ERROR: Failed to capture/send screenshot. Message: {ex.Message}");
                Console.WriteLine($"Error capturing/sending screenshot: {ex.Message}");

                // If an error occurred, ensure timers restart (if they were stopped) 
                // to continue logging activity, even if the upload failed.
                if (_oneMinuteTimer != null && !_oneMinuteTimer.Enabled)
                {
                    _oneMinuteTimer.Start();
                }
                if (_tenMinuteTimer != null && !_tenMinuteTimer.Enabled)
                {
                    _tenMinuteTimer.Start();
                }
            }
        }
    }
}