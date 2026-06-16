using System;
#if WINDOWS
using System.Runtime.InteropServices;
#endif

namespace ScreenTracker1.Services
{
    public class NativeThemeService
    {
#if WINDOWS
        private static IntPtr _windowHandle = IntPtr.Zero;

        public static void SetWindowHandle(IntPtr hwnd)
        {
            _windowHandle = hwnd;
        }

        public void SetDarkMode(bool isDark)
        {
            if (_windowHandle == IntPtr.Zero)
                return;

            try
            {
                int useDarkMode = isDark ? 1 : 0;
                DwmSetWindowAttribute(
                    _windowHandle,
                    DWMWA_USE_IMMERSIVE_DARK_MODE,
                    ref useDarkMode,
                    Marshal.SizeOf(typeof(int)));
            }
            catch
            {
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
#else
        public static void SetWindowHandle(IntPtr hwnd) { }

        public void SetDarkMode(bool isDark) { }
#endif
    }
}
