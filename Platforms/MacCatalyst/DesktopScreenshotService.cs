using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CoreGraphics;
using Foundation;
using ImageIO;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using ObjCRuntime;
using ScreenTracker1.Services;

namespace ScreenTracker1.Platforms.MacCatalyst
{
    public class DesktopScreenshotService : IScreenshotService
    {
        private static bool _permissionCheckedThisSession = false;
        private static bool _screenRecordingReady = false;
        private static readonly object LockScreenCacheSync = new();
        private static byte[]? _cachedLockScreen;

        // Keep true only for debugging. Set false in production.
        private static bool _debugPopupEnabled = true;

        private const string CoreGraphicsFramework =
            "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
        private const string CoreFoundationFramework =
            "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        private const uint kCGWindowListOptionOnScreenOnly = 1;
        private const uint kCGNullWindowID = 0;
        private const uint kCGWindowImageDefault = 0;
        private const string LockedSessionKey = "CGSSessionScreenIsLocked";

        private static readonly Lazy<byte[]> SleepScreenPlaceholder = new(() =>
        {
            try
            {
                using Stream stream = FileSystem.Current
                    .OpenAppPackageFileAsync("device-sleeping.png")
                    .GetAwaiter()
                    .GetResult();
                using var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MacScreenshot] Unable to load sleep placeholder: {ex.Message}");
                return Array.Empty<byte>();
            }
        });

        [DllImport(CoreGraphicsFramework)]
        private static extern bool CGPreflightScreenCaptureAccess();

        [DllImport(CoreGraphicsFramework)]
        private static extern bool CGRequestScreenCaptureAccess();

        [DllImport(CoreGraphicsFramework)]
        private static extern IntPtr CGSessionCopyCurrentDictionary();

        [DllImport(CoreFoundationFramework)]
        private static extern void CFRelease(IntPtr value);

        [DllImport(CoreGraphicsFramework)]
        private static extern IntPtr CGWindowListCreateImage(
            CGRect screenBounds,
            uint listOption,
            uint windowID,
            uint imageOption
        );

        public byte[] CaptureDesktop()
        {
            bool sessionLocked = IsSessionLocked();
            if (sessionLocked)
                Console.WriteLine("[MacScreenshot] Session locked; attempting a fresh native lock-screen capture.");

            if (!_screenRecordingReady)
            {
                // Lock/sleep can make screen capture temporarily unavailable.
                // Recheck on every scheduled retry so capture resumes after unlock
                // instead of remaining disabled until the app is restarted.
                _screenRecordingReady = IsScreenCapturePermissionGranted();
                if (!_screenRecordingReady)
                {
                    return sessionLocked ? GetCachedLockScreen() : Array.Empty<byte>();
                }
            }

            if (!IsScreenCapturePermissionGranted())
            {
                _screenRecordingReady = false;
                return sessionLocked ? GetCachedLockScreen() : Array.Empty<byte>();
            }

            try
            {
                IntPtr imageHandle = CGWindowListCreateImage(
                    CGRect.Infinite,
                    kCGWindowListOptionOnScreenOnly,
                    kCGNullWindowID,
                    kCGWindowImageDefault
                );

                if (imageHandle == IntPtr.Zero)
                {
                    ShowDebugPopup(
                        "Screenshot Failed",
                        "Native macOS capture returned no image."
                    );

                    return sessionLocked ? GetCachedLockScreen() : Array.Empty<byte>();
                }

                using CGImage? image = Runtime.GetINativeObject<CGImage>(
                    imageHandle,
                    true
                );

                if (image == null)
                {
                    ShowDebugPopup(
                        "Screenshot Failed",
                        "Unable to convert native screenshot image."
                    );

                    return sessionLocked ? GetCachedLockScreen() : Array.Empty<byte>();
                }

                byte[] pngBytes = ConvertCGImageToPng(image);

                if (pngBytes == null || pngBytes.Length < 2000)
                {
                    ShowDebugPopup(
                        "Invalid Screenshot",
                        $"Screenshot size is too small: {pngBytes?.Length ?? 0} bytes.\n\nmacOS did not return a real screen image."
                    );

                    return sessionLocked ? GetCachedLockScreen() : Array.Empty<byte>();
                }

                if (sessionLocked)
                {
                    lock (LockScreenCacheSync)
                        _cachedLockScreen = pngBytes.ToArray();
                    Console.WriteLine("[MacScreenshot] Valid lock-screen image cached.");
                }

                return pngBytes;
            }
            catch (Exception ex)
            {
                ShowDebugPopup(
                    "Screenshot Error",
                    ex.Message
                );

                return sessionLocked ? GetCachedLockScreen() : Array.Empty<byte>();
            }
        }

        public static byte[] GetCachedLockScreen()
        {
            lock (LockScreenCacheSync)
                return _cachedLockScreen?.ToArray() ?? Array.Empty<byte>();
        }

        public static void TryCacheCurrentLockScreen()
        {
            if (!IsSessionLocked())
                return;

            _ = new DesktopScreenshotService().CaptureDesktop();
        }

        public static byte[] GetSleepPlaceholder()
        {
            return SleepScreenPlaceholder.Value.ToArray();
        }

        public byte[] CaptureDisplay(uint displayId)
        {
            return CaptureDesktop();
        }

        public static async Task<bool> EnsureScreenRecordingPermissionAtLoginAsync()
        {
            if (_permissionCheckedThisSession)
            {
                return _screenRecordingReady;
            }

            _permissionCheckedThisSession = true;

            bool hasPermission = IsScreenCapturePermissionGranted();

            if (hasPermission)
            {
                _screenRecordingReady = true;
                return true;
            }

            bool continueRequest = await ShowConfirmPopupAsync(
                "Screen Recording Permission Required",
                "ScreenTracker1 needs Screen Recording permission to capture real screenshots.\n\nClick Continue and allow permission in macOS.\n\nAfter enabling permission, quit and restart the app.",
                "Continue",
                "Cancel"
            );

            if (!continueRequest)
            {
                _screenRecordingReady = false;
                return false;
            }

            try
            {
                CGRequestScreenCaptureAccess();
            }
            catch
            {
            }

            await Task.Delay(1000);

            hasPermission = IsScreenCapturePermissionGranted();

            if (hasPermission)
            {
                _screenRecordingReady = true;
                return true;
            }

            _screenRecordingReady = false;

            bool openSettings = await ShowConfirmPopupAsync(
                "Permission Not Active",
                "Screen Recording permission is not active yet.\n\nPlease enable ScreenTracker1 in:\n\nSystem Settings > Privacy & Security > Screen & System Audio Recording\n\nThen quit and restart the app.",
                "Open Settings",
                "Cancel"
            );

            if (openSettings)
            {
                OpenScreenRecordingSettings();
            }

            return false;
        }

        public static bool IsScreenCapturePermissionGranted()
        {
            try
            {
                return CGPreflightScreenCaptureAccess();
            }
            catch
            {
                return false;
            }
        }

        private static bool IsSessionLocked()
        {
            IntPtr dictionaryHandle = IntPtr.Zero;
            try
            {
                dictionaryHandle = CGSessionCopyCurrentDictionary();
                if (dictionaryHandle == IntPtr.Zero)
                    return false;

                using NSDictionary? dictionary =
                    Runtime.GetNSObject<NSDictionary>(dictionaryHandle, true);
                dictionaryHandle = IntPtr.Zero; // ownership transferred above

                using var key = new NSString(LockedSessionKey);
                return dictionary?[key] is NSNumber locked && locked.BoolValue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MacScreenshot] Unable to read lock state: {ex.Message}");
                return false;
            }
            finally
            {
                if (dictionaryHandle != IntPtr.Zero)
                    CFRelease(dictionaryHandle);
            }
        }

        public static void OpenScreenRecordingSettings()
        {
            try
            {
                Process.Start(
                    "open",
                    "x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture"
                );
            }
            catch
            {
            }
        }

        public static void ResetPermissionStateForDebug()
        {
            _permissionCheckedThisSession = false;
            _screenRecordingReady = false;
        }

        private static byte[] ConvertCGImageToPng(CGImage image)
        {
            using NSMutableData data = new NSMutableData();

            using CGImageDestination? destination =
                CGImageDestination.Create(
                    data,
                    "public.png",
                    1
                );

            if (destination == null)
            {
                return Array.Empty<byte>();
            }

            destination.AddImage(image);
            destination.Close();

            return data.ToArray();
        }

        private static void ShowDebugPopup(string title, string message)
        {
            if (!_debugPopupEnabled)
                return;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    if (Application.Current?.MainPage != null)
                    {
                        await Application.Current.MainPage.DisplayAlert(
                            title,
                            message,
                            "OK"
                        );
                    }
                }
                catch
                {
                }
            });
        }

        private static Task<bool> ShowConfirmPopupAsync(
            string title,
            string message,
            string accept,
            string cancel
        )
        {
            return MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (Application.Current?.MainPage != null)
                {
                    return await Application.Current.MainPage.DisplayAlert(
                        title,
                        message,
                        accept,
                        cancel
                    );
                }

                return false;
            });
        }
    }
}
