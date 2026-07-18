using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ScreenTracker1.Platforms.MacCatalyst
{
    public class GlobalInputMonitorService : IDisposable
    {
        private const string CoreGraphicsLibrary = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

        public event Action? OnMouseDown;
        public event Action? OnKeyDown;

        public int KeyboardClickCount { get; private set; }
        public int MouseClickCount { get; private set; }

        private System.Timers.Timer? _pollTimer;
        private double _lastKbdTime;
        private double _lastMouseTime;
        public void Start()
        {
            _lastKbdTime = CGEventSourceSecondsSinceLastEventType(IntPtr.Zero, kCGEventKeyDown);
            _lastMouseTime = CGEventSourceSecondsSinceLastEventType(IntPtr.Zero, kCGEventLeftMouseDown);

            _pollTimer = new System.Timers.Timer(250);
            _pollTimer.Elapsed += (s, e) => PollInput();
            _pollTimer.Start();
        }

        private void PollInput()
        {
            double nowKbd = CGEventSourceSecondsSinceLastEventType(IntPtr.Zero, kCGEventKeyDown);
            double nowMouse = CGEventSourceSecondsSinceLastEventType(IntPtr.Zero, kCGEventLeftMouseDown);

            // CGEventSourceSecondsSinceLastEventType returns seconds since the last event.
            // When a new event occurs, this value resets to near 0 (drops sharply).
            // Use a threshold to filter out floating-point jitter.
            if (nowKbd < _lastKbdTime - 0.05)
            {
                KeyboardClickCount++;
                MacIdleTimeService.ReportActivity();
                MacIdleTimeService.SaveActivity();
                OnKeyDown?.Invoke();
            }

            if (nowMouse < _lastMouseTime - 0.05)
            {
                MouseClickCount++;
                MacIdleTimeService.ReportActivity();
                MacIdleTimeService.SaveActivity();
                OnMouseDown?.Invoke();
            }

            _lastKbdTime = nowKbd;
            _lastMouseTime = nowMouse;
        }

        public (int keyboard, int mouse) GetAndResetClicks()
        {
            int kbd = KeyboardClickCount;
            int mouse = MouseClickCount;
            KeyboardClickCount = 0;
            MouseClickCount = 0;
            return (kbd, mouse);
        }

        public static (double x, double y) GetMousePosition()
        {
            return (0, 0);
        }

        public static bool IsAccessibilityPermissionGranted()
        {
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

        public static void OpenAccessibilitySettings()
        {
            try
            {
                Process.Start("open", "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility");
            }
            catch { }
        }

        public static void OpenScreenRecordingSettings()
        {
            try
            {
                Process.Start("open", "x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture");
            }
            catch { }
        }

        [DllImport(CoreGraphicsLibrary)]
        private static extern double CGEventSourceSecondsSinceLastEventType(IntPtr source, uint eventType);

        private const uint kCGEventKeyDown = 10;
        private const uint kCGEventLeftMouseDown = 1;

        public void Stop()
        {
            _pollTimer?.Stop();
            _pollTimer?.Dispose();
            _pollTimer = null;
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }
    }
}
