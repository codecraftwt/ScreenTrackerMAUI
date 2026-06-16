using System;
using System.Threading.Tasks;
using System.Timers;
#if MACCATALYST
using ScreenTracker1.Platforms.MacCatalyst;
#endif

namespace ScreenTracker1.Services
{
    /// <summary>
    /// Cross-platform orchestrator that runs the appropriate tracking
    /// services for macOS via Mac Catalyst.
    /// On Windows, existing services are used instead.
    /// 
    /// Usage from Blazor: inject this service, call Start() on macOS,
    /// and it will handle window tracking, input monitoring, screenshots,
    /// and periodic uploads matching the Windows DesktopAutoCaptureService behavior.
    /// </summary>
    public class MacServicesInitializer : IDisposable
    {
#if MACCATALYST
        private ActiveWindowTrackerService? _activeWindowTracker;
        private GlobalInputMonitorService? _inputMonitor;
        private DesktopScreenshotService? _screenshotService;

        private System.Timers.Timer? _oneMinuteTimer;
        private System.Timers.Timer? _tenMinuteTimer;

        private int _currentMinute = 0;
        private int _keyboardClicks = 0;
        private int _mouseClicks = 0;

        private readonly System.Collections.Generic.Dictionary<int, (int keyboard, int mouse, DateTime timestamp)> _minuteStats =
            new System.Collections.Generic.Dictionary<int, (int, int, DateTime)>();

        public event Action<string>? OnLog;

        private int _userId;
        private string _token = string.Empty;
        private string _userName = "UnknownUser";
        private string _startMode = "manual";
#endif

        /// <summary>
        /// Start all tracking services for macOS.
        /// </summary>
        public void Start(string startMode, int userId = 0, string token = "", string userName = "UnknownUser")
        {
#if MACCATALYST
            _userId = userId;
            _token = token;
            _userName = userName;
            _startMode = startMode;

            OnLog?.Invoke($"[MacServices] Starting macOS tracking services for user: {_userName}...");

            // 1. Check & handle permissions
            bool accessibilityGranted = GlobalInputMonitorService.IsAccessibilityPermissionGranted();
            if (!accessibilityGranted)
            {
                OnLog?.Invoke("[MacServices] ⚠️ Accessibility permission NOT granted. Opening System Settings...");
                GlobalInputMonitorService.OpenAccessibilitySettings();
            }
            else
            {
                OnLog?.Invoke("[MacServices] ✅ Accessibility permission is granted.");
            }

            // 2. Active Window Tracker
            _activeWindowTracker = new ActiveWindowTrackerService();
            _activeWindowTracker.OnActiveWindowChanged += (appName, title) =>
            {
                OnLog?.Invoke($"[ActiveWindow] {appName} - \"{title}\"");
            };
            _activeWindowTracker.Start();
            OnLog?.Invoke("[MacServices] ✅ Active window tracker started.");

            // 3. Global Input Monitor
            _inputMonitor = new GlobalInputMonitorService();
            _inputMonitor.Start();
            OnLog?.Invoke("[MacServices] ✅ Global input monitor started.");

            // 4. Screenshot Service
            _screenshotService = new DesktopScreenshotService();
            OnLog?.Invoke("[MacServices] ✅ Screenshot service initialized.");

            // 5. Timers (matching Windows DesktopAutoCaptureService: 1-min + 10-min cycles)
            _oneMinuteTimer = new System.Timers.Timer(TimeSpan.FromMinutes(1).TotalMilliseconds);
            _oneMinuteTimer.Elapsed += OnOneMinuteElapsed;
            _oneMinuteTimer.AutoReset = true;
            _oneMinuteTimer.Start();

            _tenMinuteTimer = new System.Timers.Timer(TimeSpan.FromMinutes(10).TotalMilliseconds);
            _tenMinuteTimer.Elapsed += async (s, e) => await CaptureAndSendAsync();
            _tenMinuteTimer.AutoReset = true;
            _tenMinuteTimer.Start();

            OnLog?.Invoke("[MacServices] ✅ Timers started (1-min + 10-min cycles).");
            OnLog?.Invoke($"[MacServices] ✅ All macOS tracking services running (mode: {_startMode}).");
#endif
        }

#if MACCATALYST
        private void OnOneMinuteElapsed(object? sender, ElapsedEventArgs e)
        {
            if (_inputMonitor == null) return;

            var (kbd, mouse) = _inputMonitor.GetAndResetClicks();
            int index = _currentMinute;
            _minuteStats[index] = (kbd, mouse, DateTime.Now);

            OnLog?.Invoke($"[Minute {index + 1}] Keyboard: {kbd}, Mouse: {mouse}");

            _currentMinute = (_currentMinute + 1) % 10;
        }

        /// <summary>
        /// Captures screenshot and compiles activity data for the 10-minute cycle.
        /// In a production version, this would upload to the API like DesktopAutoCaptureService.CaptureAndSendAsync().
        /// </summary>
        private async Task CaptureAndSendAsync()
        {
            if (_screenshotService == null) return;

            // Stop timers for capture
            _oneMinuteTimer?.Stop();
            _tenMinuteTimer?.Stop();

            OnLog?.Invoke("[Capture] Starting 10-minute screenshot capture...");

            // Force-log the current minute's data
            if (_currentMinute > 0)
            {
                if (_inputMonitor != null)
                {
                    var (kbd, mouse) = _inputMonitor.GetAndResetClicks();
                    _minuteStats[_currentMinute] = (kbd, mouse, DateTime.Now);
                }
                _currentMinute = 0;
            }

            // Capture screenshot
            byte[] imageBytes = _screenshotService.CaptureDesktop();

            // Compile activity totals
            int totalKeyboard = 0, totalMouse = 0;
            foreach (var kvp in _minuteStats)
            {
                totalKeyboard += kvp.Value.keyboard;
                totalMouse += kvp.Value.mouse;
            }

            OnLog?.Invoke($"[Capture] Screenshot: {imageBytes.Length} bytes, Activity: {totalKeyboard} keys, {totalMouse} clicks");

            if (imageBytes.Length > 0)
            {
                // TODO: Implement API upload matching DesktopAutoCaptureService.CaptureAndSendAsync()
                // This would send to your Image/upload endpoint with multipart form data
                OnLog?.Invoke("[Capture] Screenshot captured successfully (API upload not yet implemented in this scaffold).");
            }
            else
            {
                OnLog?.Invoke("[Capture] ⚠️ Screenshot returned empty. Check Screen Recording permission.");
            }

            // Reset for next cycle
            _minuteStats.Clear();

            // Restart timers
            _oneMinuteTimer?.Start();
            _tenMinuteTimer?.Start();
            OnLog?.Invoke("[Capture] Timers restarted.");
        }

        /// <summary>
        /// Gets the current frontmost application info (for Blazor UI display).
        /// </summary>
        public (string appName, string windowTitle) GetCurrentActiveWindow()
        {
            if (_activeWindowTracker != null)
                return _activeWindowTracker.GetActiveWindowInfo();
            return ("Unknown", "");
        }

        /// <summary>
        /// Gets the current accumulated click counts and resets them.
        /// </summary>
        public (int keyboard, int mouse) GetAndResetClickCounts()
        {
            if (_inputMonitor != null)
                return _inputMonitor.GetAndResetClicks();
            return (0, 0);
        }
#endif

        /// <summary>
        /// Stop all tracking services.
        /// </summary>
        public void Stop()
        {
#if MACCATALYST
            OnLog?.Invoke("[MacServices] Stopping all macOS tracking services...");

            _oneMinuteTimer?.Stop();
            _oneMinuteTimer?.Dispose();
            _oneMinuteTimer = null;

            _tenMinuteTimer?.Stop();
            _tenMinuteTimer?.Dispose();
            _tenMinuteTimer = null;

            _inputMonitor?.Stop();
            _inputMonitor?.Dispose();
            _inputMonitor = null;

            _activeWindowTracker?.Stop();
            _activeWindowTracker?.Dispose();
            _activeWindowTracker = null;

            OnLog?.Invoke("[MacServices] ✅ All macOS tracking services stopped.");
#endif
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }
    }
}
