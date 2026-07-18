using System;
using System.Runtime.InteropServices;
using Foundation;
using AppKit;

namespace ScreenTracker1.Platforms.MacCatalyst
{
    /// <summary>
    /// Monitors global keyboard and mouse events on macOS using NSEvent.
    /// Replacement for Gma.System.MouseKeyHook on Windows.
    /// Requires Accessibility permission (System Settings > Privacy & Security > Accessibility).
    /// 
    /// NOTE: Global keyboard event monitoring via NSEvent.AddGlobalMonitorForEventsMatchingMask
    /// is blocked when App Sandbox is enabled for distribution through the Mac App Store.
    /// For Mac App Store distribution, use an XPC helper service or distribute outside the store.
    /// </summary>
    public class GlobalInputMonitorService : IDisposable
    {
        private NSObject? _keyboardMonitor;
        private NSObject? _mouseMonitor;

        /// <summary>
        /// Fires on each global mouse down event (click).
        /// </summary>
        public event Action<NSEvent>? OnMouseDown;

        /// <summary>
        /// Fires on each global key down event.
        /// </summary>
        public event Action<NSEvent>? OnKeyDown;

        /// <summary>
        /// Current accumulated keyboard click count (reset on demand).
        /// </summary>
        public int KeyboardClickCount { get; private set; }

        /// <summary>
        /// Current accumulated mouse click count (reset on demand).
        /// </summary>
        public int MouseClickCount { get; private set; }

        /// <summary>
        /// Tracks which keys are currently held down (for debouncing auto-repeat).
        /// Maps key code to the last time it was counted.
        /// </summary>
        private readonly System.Collections.Generic.Dictionary<ushort, DateTime> _keyLastCountTime =
            new System.Collections.Generic.Dictionary<ushort, DateTime>();

        private const int KeyDebounceMilliseconds = 200;

        public void Start()
        {
            // ---- Keyboard Global Monitor ----
            // NSEventMask.KeyDown monitors all key down events system-wide
            // NOTE: This will NOT work if App Sandbox is enabled
            _keyboardMonitor = NSEvent.AddGlobalMonitorForEventsMatchingMask(
                NSEventMask.KeyDown,
                eventHandler: (NSEvent evt) =>
                {
                    if (evt == null) return;

                    ushort keyCode = evt.KeyCode;
                    var now = DateTime.UtcNow;

                    // Debounce: skip if this key was counted recently
                    if (_keyLastCountTime.TryGetValue(keyCode, out var lastTime) &&
                        (now - lastTime).TotalMilliseconds < KeyDebounceMilliseconds)
                    {
                        return;
                    }

                    KeyboardClickCount++;
                    _keyLastCountTime[keyCode] = now;
                    OnKeyDown?.Invoke(evt);

                    Console.WriteLine($"[MacKeyboard] KeyDown: code={keyCode}, total={KeyboardClickCount}");
                }
            );

            // ---- Mouse Global Monitor ----
            // Monitor LeftMouseDown, RightMouseDown, and OtherMouseDown (middle/extra buttons)
            _mouseMonitor = NSEvent.AddGlobalMonitorForEventsMatchingMask(
                NSEventMask.LeftMouseDown | NSEventMask.RightMouseDown | NSEventMask.OtherMouseDown,
                eventHandler: (NSEvent evt) =>
                {
                    if (evt == null) return;

                    MouseClickCount++;
                    OnMouseDown?.Invoke(evt);

                    Console.WriteLine($"[MacMouse] MouseDown: type={evt.Type}, button={evt.ButtonNumber}, total={MouseClickCount}");
                }
            );

            Console.WriteLine("[GlobalInputMonitorService] Started monitoring global input events.");
        }

        /// <summary>
        /// Gets the current accumulated click counts and resets them to zero.
        /// </summary>
        public (int keyboard, int mouse) GetAndResetClicks()
        {
            int kbd = KeyboardClickCount;
            int mouse = MouseClickCount;
            KeyboardClickCount = 0;
            MouseClickCount = 0;
            return (kbd, mouse);
        }

        /// <summary>
        /// Gets the current mouse position on screen.
        /// </summary>
        public static (double x, double y) GetMousePosition()
        {
            var loc = NSEvent.CurrentMousePosition;
            return (loc.X, loc.Y);
        }

        #region Accessibility Permission Helpers

        /// <summary>
        /// Checks if this app has been granted Accessibility permission.
        /// </summary>
        public static bool IsAccessibilityPermissionGranted()
        {
            // Use the private Accessibility framework API
            const string axApiPath = "/System/Library/PrivateFrameworks/Accessibility.framework/Accessibility";
            IntPtr handle = dlopen(axApiPath, 0);
            if (handle == IntPtr.Zero)
                return false;

            try
            {
                IntPtr ptr = dlsym(handle, "AXIsProcessTrusted");
                if (ptr == IntPtr.Zero)
                    return false;

                var del = Marshal.GetDelegateForFunctionPointer<AXIsProcessTrustedDelegate>(ptr);
                return del();
            }
            catch
            {
                return false;
            }
            finally
            {
                dlclose(handle);
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool AXIsProcessTrustedDelegate();

        [DllImport("/usr/lib/libSystem.dylib")]
        private static extern IntPtr dlopen(string path, int mode);

        [DllImport("/usr/lib/libSystem.dylib")]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);

        [DllImport("/usr/lib/libSystem.dylib")]
        private static extern int dlclose(IntPtr handle);

        /// <summary>
        /// Opens System Settings to the Accessibility privacy pane.
        /// </summary>
        public static void OpenAccessibilitySettings()
        {
            var url = NSUrl.FromString(
                "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility"
            );
            if (url != null)
                NSWorkspace.SharedWorkspace.OpenUrl(url);
        }

        /// <summary>
        /// Opens System Settings to the Screen Recording privacy pane.
        /// </summary>
        public static void OpenScreenRecordingSettings()
        {
            var url = NSUrl.FromString(
                "x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture"
            );
            if (url != null)
                NSWorkspace.SharedWorkspace.OpenUrl(url);
        }

        #endregion

        public void Stop()
        {
            if (_keyboardMonitor != null)
            {
                NSEvent.RemoveMonitor(_keyboardMonitor);
                _keyboardMonitor = null;
            }
            if (_mouseMonitor != null)
            {
                NSEvent.RemoveMonitor(_mouseMonitor);
                _mouseMonitor = null;
            }
            _keyLastCountTime.Clear();
            Console.WriteLine("[GlobalInputMonitorService] Stopped monitoring.");
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }
    }
}
