using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace ScreenTracker1.Platforms.Windows
{
    public class WindowInterop
    {
        // P/Invoke to get the system menu
        [DllImport("user32.dll")]
        public static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        // P/Invoke to delete a menu item (close button)
        [DllImport("user32.dll")]
        public static extern bool DeleteMenu(IntPtr hMenu, int uPosition, int uFlags);

        // Constants for SC_CLOSE (Close button) and MF_BYCOMMAND
        private const int SC_CLOSE = 0xF060;
        private const int MF_BYCOMMAND = 0x00000000;

        // IntPtr to store window handle
        private IntPtr _windowHandle;

        // Constructor to get the window handle
        public WindowInterop(IntPtr hwnd)
        {
            _windowHandle = hwnd;
        }

        // Disable the close button (X) on the title bar
        public void DisableCloseButton()
        {
            var hMenu = GetSystemMenu(_windowHandle, false);
            if (hMenu != IntPtr.Zero)
            {
                DeleteMenu(hMenu, SC_CLOSE, MF_BYCOMMAND);
            }
        }
    }
}
