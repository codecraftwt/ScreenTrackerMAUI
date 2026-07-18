using System;
using System.Runtime.InteropServices;

namespace ScreenTracker1.Platforms.MacCatalyst
{
    /// <summary>
    /// Detects user idle time on macOS using Core Graphics events.
    /// Replacement for the Win32 GetLastInputInfo P/Invoke approach used in AfkTrackerService.cs.
    /// </summary>
    public static class MacIdleTimeService
    {
        private const string CoreGraphicsLibrary = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
        private const string CoreFoundationLibrary = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        // kCGEventSourceStateCombinedSessionState = -1
        private const long EventSourceStateCombinedSessionState = -1;
        // kCGAnyInputEventType = ~0 (all input event types)
        private const ulong AnyInputEventType = 0xFFFFFFFFFFFFFFFF;

        [DllImport(CoreGraphicsLibrary)]
        private static extern IntPtr CGEventSourceCreate(long stateID);

        [DllImport(CoreGraphicsLibrary)]
        private static extern double CGEventSourceSecondsSinceLastEventType(IntPtr source, ulong eventType);

        [DllImport(CoreFoundationLibrary)]
        private static extern void CFRelease(IntPtr cf);

        /// <summary>
        /// Returns the number of seconds since the last input event
        /// (keyboard or mouse) system-wide.
        /// Returns 0 on error.
        /// </summary>
        public static double GetIdleTimeInSeconds()
        {
            try
            {
                IntPtr eventSource = CGEventSourceCreate(EventSourceStateCombinedSessionState);
                if (eventSource == IntPtr.Zero)
                    return 0;

                try
                {
                    double seconds = CGEventSourceSecondsSinceLastEventType(
                        eventSource,
                        AnyInputEventType
                    );
                    return seconds;
                }
                finally
                {
                    CFRelease(eventSource);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MacIdleTime] Error getting idle time: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Returns a TimeSpan of the idle duration.
        /// </summary>
        public static TimeSpan GetIdleTime()
        {
            return TimeSpan.FromSeconds(GetIdleTimeInSeconds());
        }

        /// <summary>
        /// Checks if the user has been idle for at least the specified duration.
        /// </summary>
        public static bool IsIdleFor(TimeSpan duration)
        {
            return GetIdleTimeInSeconds() >= duration.TotalSeconds;
        }

        /// <summary>
        /// Checks if the user has been idle for at least the specified number of seconds.
        /// </summary>
        public static bool IsIdleForSeconds(double seconds)
        {
            return GetIdleTimeInSeconds() >= seconds;
        }
    }
}
