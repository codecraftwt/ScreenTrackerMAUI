using System.Runtime.InteropServices;

namespace ScreenTracker1.Platforms.MacCatalyst
{
    internal static class MacActiveWindowNative
    {
        private const uint kCGNullWindowID = 0;
        private const uint kCFStringEncodingUTF8 = 0x08000100;
        private const uint kCFNumberSInt32Type = 3;

        [Flags]
        private enum CGWindowListOption : uint
        {
            OnScreenOnly = 1 << 0,
            ExcludeDesktopElements = 1 << 4,
        }

        private static readonly IntPtr kCGWindowOwnerPID = StringToCFString("kCGWindowOwnerPID");
        private static readonly IntPtr kCGWindowName = StringToCFString("kCGWindowName");
        private static readonly IntPtr kCGWindowLayer = StringToCFString("kCGWindowLayer");
        private static readonly IntPtr kCGWindowIsOnActiveSpace = StringToCFString("kCGWindowIsOnActiveSpace");

        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern IntPtr CGWindowListCopyWindowInfo(CGWindowListOption option, uint relativeToWindow);

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern nint CFArrayGetCount(IntPtr array);

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, nint index);

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern IntPtr CFDictionaryGetValue(IntPtr dict, IntPtr key);

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern IntPtr CFStringCreateWithCString(IntPtr alloc, byte[] str, uint encoding);

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern nint CFStringGetLength(IntPtr str);

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CFStringGetCString(IntPtr str, byte[] buffer, nint bufferSize, uint encoding);

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CFNumberGetValue(IntPtr number, uint type, out int value);

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CFBooleanGetValue(IntPtr boolean);

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern void CFRelease(IntPtr obj);

        private static IntPtr StringToCFString(string str)
        {
            byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(str + "\0");
            return CFStringCreateWithCString(IntPtr.Zero, utf8, kCFStringEncodingUTF8);
        }

        private static string? CFStringToString(IntPtr cfString)
        {
            if (cfString == IntPtr.Zero) return null;
            nint length = CFStringGetLength(cfString);
            if (length == 0) return string.Empty;
            nint maxSize = length * 4 + 1;
            byte[] buffer = new byte[maxSize];
            if (CFStringGetCString(cfString, buffer, maxSize, kCFStringEncodingUTF8))
            {
                int realLen = 0;
                while (realLen < buffer.Length && buffer[realLen] != 0) realLen++;
                return System.Text.Encoding.UTF8.GetString(buffer, 0, realLen);
            }
            return null;
        }

        internal static (string appName, string windowTitle) GetActiveWindowInfo()
        {
            IntPtr windowList = IntPtr.Zero;
            try
            {
                var (frontmostPid, frontmostAppName) = GetFrontmostApplication();
                if (frontmostPid <= 0 || string.IsNullOrWhiteSpace(frontmostAppName))
                    return ("Unknown", string.Empty);

                windowList = CGWindowListCopyWindowInfo(
                    CGWindowListOption.OnScreenOnly | CGWindowListOption.ExcludeDesktopElements,
                    kCGNullWindowID);

                if (windowList == IntPtr.Zero)
                    return (frontmostAppName, string.Empty);

                nint count = CFArrayGetCount(windowList);
                if (count == 0)
                    return (frontmostAppName, string.Empty);

                for (nint i = 0; i < count; i++)
                {
                    IntPtr dict = CFArrayGetValueAtIndex(windowList, i);
                    if (dict == IntPtr.Zero) continue;

                    IntPtr ownerPidNumber = CFDictionaryGetValue(dict, kCGWindowOwnerPID);
                    if (ownerPidNumber == IntPtr.Zero ||
                        !CFNumberGetValue(ownerPidNumber, kCFNumberSInt32Type, out int ownerPid) ||
                        ownerPid != frontmostPid)
                        continue;

                    IntPtr layerNum = CFDictionaryGetValue(dict, kCGWindowLayer);
                    if (layerNum != IntPtr.Zero)
                    {
                        if (!CFNumberGetValue(layerNum, kCFNumberSInt32Type, out int layer))
                            continue;
                        if (layer != 0)
                            continue;
                    }

                    IntPtr onActiveSpace = CFDictionaryGetValue(dict, kCGWindowIsOnActiveSpace);
                    if (onActiveSpace != IntPtr.Zero)
                    {
                        if (!CFBooleanGetValue(onActiveSpace))
                            continue;
                    }

                    IntPtr windowNamePtr = CFDictionaryGetValue(dict, kCGWindowName);
                    string? windowTitle = CFStringToString(windowNamePtr);

                    return (frontmostAppName, windowTitle ?? string.Empty);
                }

                // Some applications have no ordinary layer-zero window (or macOS
                // withholds its title). The frontmost application is still valid.
                return (frontmostAppName, string.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MacActiveWindowNative] Error: {ex.Message}");
                return ("Unknown", string.Empty);
            }
            finally
            {
                if (windowList != IntPtr.Zero)
                {
                    try { CFRelease(windowList); } catch { }
                }
            }
        }

        private static (int pid, string appName) GetFrontmostApplication()
        {
            try
            {
                IntPtr workspaceClass = objc_getClass("NSWorkspace");
                IntPtr workspace = SendIntPtr(workspaceClass, "sharedWorkspace");
                IntPtr application = SendIntPtr(workspace, "frontmostApplication");
                if (application == IntPtr.Zero)
                    return (0, string.Empty);

                int pid = unchecked((int)SendNInt(application, "processIdentifier"));
                string appName = CFStringToString(SendIntPtr(application, "localizedName")) ?? string.Empty;
                return (pid, appName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MacActiveWindowNative] Frontmost application error: {ex.Message}");
                return (0, string.Empty);
            }
        }

        private static IntPtr SendIntPtr(IntPtr receiver, string selector) =>
            receiver == IntPtr.Zero ? IntPtr.Zero : objc_msgSend(receiver, sel_registerName(selector));

        private static nint SendNInt(IntPtr receiver, string selector) =>
            receiver == IntPtr.Zero ? 0 : objc_msgSend_nint(receiver, sel_registerName(selector));

        [DllImport("/usr/lib/libobjc.A.dylib")]
        private static extern IntPtr objc_getClass(string name);

        [DllImport("/usr/lib/libobjc.A.dylib")]
        private static extern IntPtr sel_registerName(string name);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        private static extern nint objc_msgSend_nint(IntPtr receiver, IntPtr selector);
    }
}
