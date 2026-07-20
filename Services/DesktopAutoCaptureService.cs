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
using Foundation;
using ObjCRuntime;
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
        private DateTime _nextCaptureUtc;

        private int _userId;
        private string token;
        private string _userName;

        private readonly object _statsLock = new object();
        private readonly object _captureLock = new object();
        private bool _isCapturing = false;

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
        private NSObject? _sleepObserver;
        private NSObject? _wakeObserver;
        private NSObject? _screenSleepObserver;
        private NSObject? _screenWakeObserver;
        private DateTime? _sleepStartedUtc;
        private volatile bool _macSleeping;
        private long _sleepPlaceholderThroughUtcTicks;
        private DateTime _lastCapturePollUtc;
        private NSObject? _systemSleepActivity;
        private MacLockNotificationObserver? _lockNotificationObserver;
#endif
        private string _startMode;

        private DateTime _lastActivityTime = DateTime.Now;
        private bool _isAfk = false;
        private const int AfkThresholdMinutes = 5;

        public bool IsRunning => _oneMinuteTimer != null && _tenMinuteTimer != null;

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

            lock (_statsLock)
            {
                _keyboardClicks = 0;
                _mouseClicks = 0;
                _currentMinute = 0;
                _minuteStats.Clear();
            }

#if WINDOWS
            _keyboardMouseEvents = Hook.GlobalEvents();

            _keyboardMouseEvents.KeyDown += (s, e) => {
                if (_activeKeys.Add((int)e.KeyCode))
                {
                    lock (_statsLock)
                    {
                        _keyboardClicks++;
                    }
                    _lastActivityTime = DateTime.Now;
                }
            };

            _keyboardMouseEvents.KeyUp += (s, e) => {
                _activeKeys.Remove((int)e.KeyCode);
            };

            _keyboardMouseEvents.MouseDown += (s, e) => {
                lock (_statsLock)
                {
                    _mouseClicks++;
                }
                _lastActivityTime = DateTime.Now;
            };
#elif MACCATALYST
            _inputMonitor = new GlobalInputMonitorService();
            _inputMonitor.OnKeyDown += () => {
                lock (_statsLock)
                {
                    _keyboardClicks++;
                }
                _lastActivityTime = DateTime.Now;
            };
            _inputMonitor.OnMouseDown += () => {
                lock (_statsLock)
                {
                    _mouseClicks++;
                }
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

            // Poll the wall-clock deadline instead of relying on one long timer.
            // macOS can delay timers because of App Nap or display sleep. A due
            // capture remains due until it uploads successfully, so a temporary
            // screen-capture/API failure is retried rather than lost for 10 minutes.
            _nextCaptureUtc = DateTime.UtcNow.AddMinutes(10);
#if MACCATALYST
            _lastCapturePollUtc = DateTime.UtcNow;
#endif
            _tenMinuteTimer = new System.Timers.Timer(TimeSpan.FromSeconds(15).TotalMilliseconds);
            _tenMinuteTimer.Elapsed += OnCaptureScheduleElapsed;
            _tenMinuteTimer.AutoReset = true;
            _tenMinuteTimer.Enabled = true;

#if MACCATALYST
            PreventMacSystemSleep();
            SetupMacSleepCapture();
#endif

            Logger.Log($"USER: {_userName}, INFO: Timers ENABLED at {DateTime.Now:HH:mm:ss.fff}. Cycle length: 10 minutes.");
        }

        private void OnOneMinuteElapsed(object sender, ElapsedEventArgs e)
        {
            lock (_statsLock)
            {
                int indexToLog = _currentMinute;

                _minuteStats[indexToLog] = (_keyboardClicks, _mouseClicks, DateTime.Now);

                Logger.Log($"USER: {_userName}, INFO: 1-Minute Tick. Logging Minute {indexToLog + 1}. Keyboard: {_keyboardClicks}, Mouse: {_mouseClicks}");

                _keyboardClicks = 0;
                _mouseClicks = 0;

                _currentMinute = (_currentMinute + 1) % 10;
            }
        }

        private async void OnCaptureScheduleElapsed(object? sender, ElapsedEventArgs e)
        {
            DateTime nowUtc = DateTime.UtcNow;

#if MACCATALYST
            TimeSpan schedulerGap = nowUtc - _lastCapturePollUtc;
            _lastCapturePollUtc = nowUtc;

            // Full macOS sleep suspends both the app and its timers. If the first
            // callback after wake is overdue, mark those deadlines as sleep slots
            // before checking the now-unlocked desktop state.
            if (schedulerGap > TimeSpan.FromSeconds(45) && nowUtc >= _nextCaptureUtc)
            {
                Interlocked.Exchange(ref _sleepPlaceholderThroughUtcTicks, nowUtc.Ticks);
                Logger.Log(
                    $"USER: {_userName}, INFO: Screenshot timer was suspended for " +
                    $"{schedulerGap.TotalMinutes:F1} minutes; overdue slots queued as sleep placeholders.");
            }
#endif

            if (nowUtc < _nextCaptureUtc)
                return;

            DateTime scheduledCaptureUtc = _nextCaptureUtc;
#if MACCATALYST
            long sleepPlaceholderThroughTicks =
                Interlocked.Read(ref _sleepPlaceholderThroughUtcTicks);
            bool isSleepCapture = _macSleeping ||
                sleepPlaceholderThroughTicks >= scheduledCaptureUtc.Ticks;
            byte[]? placeholder = isSleepCapture
                ? DesktopScreenshotService.GetCachedLockScreen()
                : null;
            bool uploaded = await CaptureAndSendCoreAsync(
                placeholder,
                isSleepCapture ? "Missed While macOS Was Asleep" : "10-Minute");
#else
            bool uploaded = await CaptureAndSendCoreAsync();
#endif
            if (!uploaded)
                return;

            // Advance from the intended slot, not from the upload/wake time. This
            // preserves 11:44, 11:54, 12:04... and lets overdue sleep slots catch up.
            _nextCaptureUtc = scheduledCaptureUtc.AddMinutes(10);
#if MACCATALYST
            if (_nextCaptureUtc.Ticks > sleepPlaceholderThroughTicks)
                Interlocked.Exchange(ref _sleepPlaceholderThroughUtcTicks, 0);
#endif
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
                _tenMinuteTimer.Elapsed -= OnCaptureScheduleElapsed;
                _tenMinuteTimer.Dispose();
                _tenMinuteTimer = null;
            }

#if WINDOWS
            _keyboardMouseEvents?.Dispose();
            _keyboardMouseEvents = null;
#elif MACCATALYST
            TeardownMacSleepCapture();
            AllowMacSystemSleep();
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

            lock (_statsLock)
            {
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
            }
            return JsonSerializer.Serialize(minuteData);
        }

        public async Task CaptureAndSendAsync()
        {
            await CaptureAndSendCoreAsync();
        }

        private async Task<bool> CaptureAndSendCoreAsync(
            byte[]? preCapturedImage = null,
            string captureReason = "10-Minute")
        {
            lock (_captureLock)
            {
                if (_isCapturing)
                {
                    Logger.Log($"USER: {_userName}, INFO: Screenshot capture already in progress. Skipping concurrency.");
                    return false;
                }
                _isCapturing = true;
            }

            try
            {
                Logger.Log($"USER: {_userName}, INFO: {captureReason} Capture started at {DateTime.Now:HH:mm:ss.fff}.");

                int totalKeyboard = 0;
                int totalMouse = 0;
                string minuteActivityJson = string.Empty;

                lock (_statsLock)
                {
                    // === ROBUSTNESS FIX: Force logging of the currently accumulating minute's data ===
                    // This handles cases where the 10-minute timer fires before the 1-minute timer's 10th tick
                    // OR if the function is called manually mid-cycle.
                    if (_currentMinute > 0)
                    {
                        int indexToCapture = _currentMinute;
                        _minuteStats[indexToCapture] = (_keyboardClicks, _mouseClicks, DateTime.Now);

                        Logger.Log($"USER: {_userName}, INFO: FORCED Log (Capture). Logging Minute {indexToCapture + 1}. Keyboard: {_keyboardClicks}, Mouse: {_mouseClicks}.");

                        // Reset state for the next cycle
                        _keyboardClicks = 0;
                        _mouseClicks = 0;
                        _currentMinute = 0;
                    }

                    totalKeyboard = _minuteStats.Values.Sum(m => m.keyboard);
                    totalMouse = _minuteStats.Values.Sum(m => m.mouse);
                    minuteActivityJson = SerializeMinuteData();
                }

                // 1. Capture Screenshot
                var imageBytes = preCapturedImage ?? _screenshotService.CaptureDesktop();
                if (imageBytes == null || imageBytes.Length == 0)
                {
                    Logger.Log($"USER: {_userName}, WARNING: Captured screenshot is empty. Skipping upload.");
                    return false;
                }

                // 2. Prepare Payload
                var content = new MultipartFormDataContent();
                var byteContent = new ByteArrayContent(imageBytes);
                byteContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");

                // File content
                content.Add(
                    byteContent,
                    "file",
                    $"desktop_screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                // User ID
                content.Add(new StringContent(_userId.ToString()), "userId");

                // Activity Data 
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
                lock (_statsLock)
                {
                    _minuteStats.Clear();
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"USER: {_userName}, ERROR: Failed to capture/send screenshot. Message: {ex.Message}");
                Console.WriteLine($"Error capturing/sending screenshot: {ex.Message}");
                return false;
            }
            finally
            {
                lock (_captureLock)
                {
                    _isCapturing = false;
                }
            }
        }

#if MACCATALYST
        private void PreventMacSystemSleep()
        {
            if (_systemSleepActivity != null)
                return;

            try
            {
                _systemSleepActivity = NSProcessInfo.ProcessInfo.BeginActivity(
                    NSActivityOptions.IdleSystemSleepDisabled,
                    "ScreenTracker active screenshot tracking");
                Logger.Log(
                    $"USER: {_userName}, INFO: macOS idle system sleep disabled while tracking; display sleep remains allowed.");
            }
            catch (Exception ex)
            {
                Logger.Log(
                    $"USER: {_userName}, ERROR: Unable to prevent macOS system sleep. Message: {ex.Message}");
            }
        }

        private void AllowMacSystemSleep()
        {
            if (_systemSleepActivity == null)
                return;

            try
            {
                NSProcessInfo.ProcessInfo.EndActivity(_systemSleepActivity);
                _systemSleepActivity.Dispose();
                _systemSleepActivity = null;
                Logger.Log($"USER: {_userName}, INFO: macOS system sleep restored.");
            }
            catch (Exception ex)
            {
                Logger.Log(
                    $"USER: {_userName}, ERROR: Unable to restore macOS system sleep. Message: {ex.Message}");
            }
        }

        private void SetupMacSleepCapture()
        {
            try
            {
                _lockNotificationObserver = new MacLockNotificationObserver(() =>
                {
                    _ = Task.Run(DesktopScreenshotService.TryCacheCurrentLockScreen);
                });
                NSDistributedNotificationCenter.DefaultCenter.AddObserver(
                    _lockNotificationObserver,
                    new Selector("handleLockNotification:"),
                    "com.apple.screenIsLocked",
                    null!,
                    NSNotificationSuspensionBehavior.DeliverImmediately);

                var notificationCenter = NSNotificationCenter.DefaultCenter;
                _sleepObserver = notificationCenter.AddObserver(
                    new NSString("NSWorkspaceWillSleepNotification"),
                    notification =>
                    {
                        _sleepStartedUtc ??= DateTime.UtcNow;
                        _macSleeping = true;
                        // Do not capture here: that would upload the previously visible
                        // desktop outside the fixed 10-minute screenshot schedule.
                        Logger.Log($"USER: {_userName}, INFO: macOS system sleep detected.");
                    });

                _wakeObserver = notificationCenter.AddObserver(
                    new NSString("NSWorkspaceDidWakeNotification"),
                    notification =>
                    {
                        HandleMacWake();
                    });

                _screenSleepObserver = notificationCenter.AddObserver(
                    new NSString("NSWorkspaceScreensDidSleepNotification"),
                    notification =>
                    {
                        _sleepStartedUtc ??= DateTime.UtcNow;
                        _macSleeping = true;
                        Logger.Log($"USER: {_userName}, INFO: macOS display sleep detected.");
                    });

                _screenWakeObserver = notificationCenter.AddObserver(
                    new NSString("NSWorkspaceScreensDidWakeNotification"),
                    notification =>
                    {
                        HandleMacWake();
                    });
            }
            catch (Exception ex)
            {
                Logger.Log($"USER: {_userName}, ERROR: Failed to register macOS sleep capture. Message: {ex.Message}");
            }
        }

        private void HandleMacWake()
        {
            DateTime wakeUtc = DateTime.UtcNow;
            if (_sleepStartedUtc.HasValue &&
                _sleepStartedUtc.Value <= _nextCaptureUtc &&
                wakeUtc >= _nextCaptureUtc)
            {
                Interlocked.Exchange(ref _sleepPlaceholderThroughUtcTicks, wakeUtc.Ticks);
                Logger.Log(
                    $"USER: {_userName}, INFO: Scheduled capture at " +
                    $"{_nextCaptureUtc.ToLocalTime():HH:mm:ss} occurred during macOS sleep; " +
                    "sleep placeholder queued.");
            }

            _macSleeping = false;
            _sleepStartedUtc = null;
        }

        private void TeardownMacSleepCapture()
        {
            if (_lockNotificationObserver != null)
            {
                NSDistributedNotificationCenter.DefaultCenter.RemoveObserver(
                    _lockNotificationObserver,
                    "com.apple.screenIsLocked",
                    null!);
                _lockNotificationObserver.Dispose();
                _lockNotificationObserver = null;
            }

            var notificationCenter = NSNotificationCenter.DefaultCenter;
            if (_sleepObserver != null)
            {
                notificationCenter.RemoveObserver(_sleepObserver);
                _sleepObserver.Dispose();
                _sleepObserver = null;
            }

            if (_wakeObserver != null)
            {
                notificationCenter.RemoveObserver(_wakeObserver);
                _wakeObserver.Dispose();
                _wakeObserver = null;
            }

            if (_screenSleepObserver != null)
            {
                notificationCenter.RemoveObserver(_screenSleepObserver);
                _screenSleepObserver.Dispose();
                _screenSleepObserver = null;
            }

            if (_screenWakeObserver != null)
            {
                notificationCenter.RemoveObserver(_screenWakeObserver);
                _screenWakeObserver.Dispose();
                _screenWakeObserver = null;
            }

            _sleepStartedUtc = null;
            _macSleeping = false;
            Interlocked.Exchange(ref _sleepPlaceholderThroughUtcTicks, 0);
        }

        private sealed class MacLockNotificationObserver : NSObject
        {
            private readonly Action _onLock;

            public MacLockNotificationObserver(Action onLock)
            {
                _onLock = onLock;
            }

            [Export("handleLockNotification:")]
            private void HandleLockNotification(NSNotification notification)
            {
                _onLock();
            }
        }
#endif
    }
}
