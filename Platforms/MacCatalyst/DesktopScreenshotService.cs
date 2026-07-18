using System;
using System.IO;
using System.Runtime.InteropServices;
using CoreGraphics;
using AppKit;
using Foundation;

namespace ScreenTracker1.Platforms.MacCatalyst
{
    /// <summary>
    /// Captures the desktop/main display on macOS using Core Graphics.
    /// Replacement for the Windows System.Drawing.Graphics.CopyFromScreen approach.
    /// Requires Screen Recording permission in System Settings (Privacy & Security > Screen Recording).
    /// </summary>
    public class DesktopScreenshotService : IScreenshotService
    {
        // Framework paths for P/Invoke
        private const string CoreGraphicsLibrary = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
        private const string CoreFoundationLibrary = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        [DllImport(CoreGraphicsLibrary)]
        private static extern uint CGMainDisplayID();

        [DllImport(CoreGraphicsLibrary)]
        private static extern IntPtr CGDisplayCreateImage(uint displayID);

        [DllImport(CoreFoundationLibrary)]
        private static extern void CFRelease(IntPtr cf);

        /// <summary>
        /// Captures the entire main display as a PNG byte array.
        /// Returns an empty array if the capture fails or permissions are denied.
        /// </summary>
        public byte[] CaptureDesktop()
        {
            try
            {
                uint mainDisplayId = CGMainDisplayID();
                IntPtr imageHandle = CGDisplayCreateImage(mainDisplayId);

                if (imageHandle == IntPtr.Zero)
                {
                    Console.WriteLine("[MacScreenshot] CGDisplayCreateImage returned null (Screen Recording permission denied?)");
                    return Array.Empty<byte>();
                }

                try
                {
                    // Wrap the native handle in a CGImage for .NET consumption
                    using var image = new CGImage(imageHandle);

                    // Convert CGImage to PNG byte array via NSBitmapImageRep
                    using var imageRep = new NSBitmapImageRep(image);
                    using var data = imageRep.RepresentationUsingType(
                        NSBitmapImageFileType.Png,
                        new NSDictionary()
                    );

                    if (data == null)
                    {
                        Console.WriteLine("[MacScreenshot] Failed to convert CGImage to PNG data");
                        return Array.Empty<byte>();
                    }

                    byte[] result = data.ToArray();
                    Console.WriteLine($"[MacScreenshot] Captured main display ({mainDisplayId}): {result.Length} bytes");
                    return result;
                }
                finally
                {
                    // Release the native CGImage ref
                    CFRelease(imageHandle);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MacScreenshot] Error capturing desktop: {ex.Message}");
                return Array.Empty<byte>();
            }
        }

        /// <summary>
        /// Captures a screenshot of a specific display by ID.
        /// </summary>
        public byte[] CaptureDisplay(uint displayId)
        {
            try
            {
                IntPtr imageHandle = CGDisplayCreateImage(displayId);
                if (imageHandle == IntPtr.Zero)
                    return Array.Empty<byte>();

                try
                {
                    using var image = new CGImage(imageHandle);
                    using var imageRep = new NSBitmapImageRep(image);
                    using var data = imageRep.RepresentationUsingType(
                        NSBitmapImageFileType.Png,
                        new NSDictionary()
                    );

                    if (data == null)
                        return Array.Empty<byte>();

                    return data.ToArray();
                }
                finally
                {
                    CFRelease(imageHandle);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MacScreenshot] Error capturing display {displayId}: {ex.Message}");
                return Array.Empty<byte>();
            }
        }

        /// <summary>
        /// Checks if screen capture permission has been granted by attempting a test capture.
        /// On macOS 10.15+, this requires Screen Recording permission.
        /// </summary>
        public static bool IsScreenCapturePermissionGranted()
        {
            try
            {
                uint mainDisplayId = CGMainDisplayID();
                IntPtr imageHandle = CGDisplayCreateImage(mainDisplayId);

                if (imageHandle == IntPtr.Zero)
                    return false;

                CFRelease(imageHandle);
                return true;
            }
            catch
            {
                return false;
            }
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
    }
}
