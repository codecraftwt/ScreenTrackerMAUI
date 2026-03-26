using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Gma.System.MouseKeyHook;
using Microsoft.Maui.ApplicationModel;
using System.IO;
using System.Net.Http;
using System.Timers;
using ScreenTracker1.Platforms.Windows;

namespace ScreenTracker1.Services
{
    public class DesktopAutoCaptureService
    {
        private readonly DesktopScreenshotService _screenshotService;
        private readonly HttpClient _httpClient;
        private System.Timers.Timer _oneMinuteTimer;
        private System.Timers.Timer _tenMinuteTimer;

        private int _userId;
        private string token;
        private string _userName;

        private int _currentMinute = 0;
        private int _keyboardClicks = 0;
        private int _mouseClicks = 0;
        // This field was correctly added by you
        private HashSet<System.Windows.Forms.Keys> _activeKeys = new HashSet<System.Windows.Forms.Keys>();


        private Dictionary<int, (int keyboard, int mouse, DateTime timestamp)> _minuteStats =
            new Dictionary<int, (int, int, DateTime)>();

        private IKeyboardMouseEvents _keyboardMouseEvents;
        private string _startMode;

        private DateTime _lastActivityTime = DateTime.Now;
        private bool _isAfk = false;
        private const int AfkThresholdMinutes = 5;

        public DesktopAutoCaptureService(DesktopScreenshotService screenshotService)
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

            _keyboardMouseEvents = Hook.GlobalEvents();

            // ----------------------------------------------------
            // 🚨 FIX: REPLACE KeyPress with KeyDown/KeyUp logic 🚨
            // ----------------------------------------------------

            // 1. KeyDown: Only count if the key is NOT already in the set (i.e., not auto-repeat)
            _keyboardMouseEvents.KeyDown += (s, e) => {
                // .Add() returns true if the key was successfully added (was not present)
                if (_activeKeys.Add(e.KeyCode))
                {
                    _keyboardClicks++;
                    _lastActivityTime = DateTime.Now;
                }
            };

            // 2. KeyUp: Remove the key from the set when released
            _keyboardMouseEvents.KeyUp += (s, e) => {
                _activeKeys.Remove(e.KeyCode);
            };
            // ----------------------------------------------------


            _keyboardMouseEvents.MouseDown += (s, e) => {
                _mouseClicks++;
                _lastActivityTime = DateTime.Now;
            };

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

            _keyboardMouseEvents?.Dispose();
            _keyboardMouseEvents = null;

            // ------------------------------------
            // 🚨 FIX: Clear the tracking state 🚨
            // ------------------------------------
            _activeKeys.Clear();
            // ------------------------------------

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