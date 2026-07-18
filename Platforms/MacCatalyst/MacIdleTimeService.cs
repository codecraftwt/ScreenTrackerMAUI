using System;
using System.Runtime.InteropServices;

namespace ScreenTracker1.Platforms.MacCatalyst
{
    public static class MacIdleTimeService
    {
        private const string CoreGraphicsLibrary = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
        // CGEventSourceStateID: 0 = combined session state.
        private const int EventSourceStateCombinedSessionState = 0;
        private const uint AnyInputEventType = 0xFFFFFFFF;
        private const string LastActivityPrefKey = "MacLastActivityTime";

        [DllImport(CoreGraphicsLibrary)]
        private static extern double CGEventSourceSecondsSinceLastEventType(int stateID, uint eventType);

        private static DateTime _lastActivityTime = DateTime.UtcNow;
        private static readonly object _lock = new object();

        static MacIdleTimeService()
        {
            DateTime? persisted = GetLastPersistedActivityTime();
            if (persisted.HasValue)
            {
                _lastActivityTime = persisted.Value;
                Console.WriteLine($"[MacIdleTime] Initialized from persisted: {_lastActivityTime:O}");
            }
        }

        public static void ReportActivity()
        {
            lock (_lock)
            {
                _lastActivityTime = DateTime.UtcNow;
            }
        }

        public static void SaveActivity()
        {
            var now = DateTime.UtcNow;
            lock (_lock)
            {
                _lastActivityTime = now;
            }
            Preferences.Set(LastActivityPrefKey, now.ToString("O"));
        }

        public static DateTime? GetLastPersistedActivityTime()
        {
            string saved = Preferences.Get(LastActivityPrefKey, null);
            if (saved != null && DateTime.TryParse(saved, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                return dt;
            return null;
        }

        public static double GetSleepAwareIdleTimeInSeconds()
        {
            DateTime? persisted = GetLastPersistedActivityTime();
            if (persisted.HasValue)
            {
                double seconds = (DateTime.UtcNow - persisted.Value).TotalSeconds;
                Console.WriteLine($"[MacIdleTime] Sleep-aware idle: {TimeSpan.FromSeconds(seconds):hh\\:mm\\:ss}");
                return seconds;
            }
            return GetIdleTimeInSeconds();
        }

        public static double GetIdleTimeInSeconds()
        {
            try
            {
                // This API accepts a CGEventSourceStateID value, not a
                // CGEventSourceRef pointer. Passing a created event-source pointer
                // can deadlock SkyLight and display the macOS spinning beachball.
                double seconds = CGEventSourceSecondsSinceLastEventType(
                    EventSourceStateCombinedSessionState,
                    AnyInputEventType);
                if (seconds >= 0)
                    return seconds;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MacIdleTime] Error getting idle time: {ex.Message}");
            }

            lock (_lock)
            {
                return (DateTime.UtcNow - _lastActivityTime).TotalSeconds;
            }
        }

        public static TimeSpan GetIdleTime()
        {
            return TimeSpan.FromSeconds(GetIdleTimeInSeconds());
        }

        public static bool IsIdleFor(TimeSpan duration)
        {
            return GetIdleTimeInSeconds() >= duration.TotalSeconds;
        }

        public static bool IsIdleForSeconds(double seconds)
        {
            return GetIdleTimeInSeconds() >= seconds;
        }
    }
}
