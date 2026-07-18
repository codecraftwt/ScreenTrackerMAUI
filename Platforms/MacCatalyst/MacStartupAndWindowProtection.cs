#if MACCATALYST
using Foundation;
using ObjCRuntime;
using System.Runtime.InteropServices;

namespace ScreenTracker1.Platforms.MacCatalyst;

/// <summary>
/// Adds the Mac equivalents of launch-at-login, maximize, and suppression of
/// ordinary close/quit UI. System termination requests are left to macOS so
/// this application cannot interrupt logout, restart, or shutdown.
/// </summary>
internal static class MacStartupAndWindowProtection
{
    private const string ObjectiveCLibrary = "/usr/lib/libobjc.A.dylib";
    private const long CloseButton = 0;
    private const long ZoomButton = 2;
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library",
        "Logs",
        "ScreenTrackerProtection.log");

    private static NSTimer? protectionTimer;
    private static IntPtr protectedApplicationClass;
    private static IntPtr protectedApplicationDelegateClass;
    private static IntPtr originalTerminateImplementation;
    private static bool loggedApplicationFound;
    private static bool loggedNoApplication;
    private static bool loggedWindows;
    private static bool systemTerminationRequested;
    private static readonly ApplicationTerminateDelegate applicationTerminateHandler =
        ApplicationTerminate;
    private static readonly ApplicationShouldTerminateDelegate applicationShouldTerminateHandler =
        ApplicationShouldTerminate;
    private static readonly WorkspaceWillPowerOffDelegate workspaceWillPowerOffHandler =
        WorkspaceWillPowerOff;

    internal static void Initialize()
    {
        Log($"Protection initialized. OS={Environment.OSVersion}; App={NSBundle.MainBundle.BundleIdentifier}");
        RegisterLaunchAtLogin();

        // The native AppKit window is created after FinishedLaunching. Reapply
        // protection because Mac Catalyst can replace it during scene changes.
        protectionTimer ??= NSTimer.CreateRepeatingScheduledTimer(
            TimeSpan.FromSeconds(1),
            _ => ProtectNativeWindowAndQuitCommand());
    }

    private static void RegisterLaunchAtLogin()
    {
        // SMAppService is available to Mac Catalyst starting with version 16,
        // which corresponds to macOS 13's modern Login Items implementation.
        if (!OperatingSystem.IsMacCatalystVersionAtLeast(16))
            return;

        try
        {
            // The .NET 8 binding marks this macOS API as unavailable to Mac
            // Catalyst even though it is present in the host process. Resolve
            // it dynamically, as we already do for the native AppKit window.
            NSBundle.FromPath("/System/Library/Frameworks/ServiceManagement.framework")?.Load();
            var serviceClass = objc_getClass("SMAppService");
            var service = SendIntPtr(serviceClass, "mainAppService");

            // SMAppServiceStatusEnabled == 1. NSError** may be NULL here; a
            // declined request is non-fatal and can be approved in Settings.
            if (service != IntPtr.Zero && SendLong(service, "status") != 1)
                SendBoolIntPtr(service, "registerAndReturnError:", IntPtr.Zero);
        }
        catch (Exception exception)
        {
            // Registration can require approval in System Settings. Do not
            // prevent the tracker from starting if macOS declines the request.
            System.Diagnostics.Debug.WriteLine(
                $"Unable to register ScreenTracker as a login item: {exception}");
        }
    }

    private static void ProtectNativeWindowAndQuitCommand()
    {
        try
        {
            var application = GetSharedApplication();
            if (application == IntPtr.Zero)
            {
                if (!loggedNoApplication)
                {
                    Log("NSApplication sharedApplication not found");
                    loggedNoApplication = true;
                }

                return;
            }

            if (!loggedApplicationFound)
            {
                Log($"NSApplication found. class={GetClassName(object_getClass(application))}");
                loggedApplicationFound = true;
            }

            InstallApplicationTerminateProtection(application);
            InstallTerminationProtection(application);
            ProtectAllNativeWindows(application);

            DisableQuitItems(SendIntPtr(application, "mainMenu"));
        }
        catch (Exception exception)
        {
            Log($"Protection error: {exception}");
            System.Diagnostics.Debug.WriteLine(
                $"Unable to apply Mac window protection: {exception}");
        }
    }

    private static void InstallTerminationProtection(IntPtr application)
    {
        var applicationDelegate = SendIntPtr(application, "delegate");
        if (applicationDelegate == IntPtr.Zero)
        {
            Log("NSApplication delegate not found");
            return;
        }

        var delegateClass = object_getClass(applicationDelegate);
        if (delegateClass == IntPtr.Zero || delegateClass == protectedApplicationDelegateClass)
            return;

        // Dock > Quit does not use NSApplication.mainMenu. It asks the native
        // application delegate whether termination should proceed, so override
        // that delegate callback and cancel ordinary termination requests.
        var implementation = Marshal.GetFunctionPointerForDelegate(applicationShouldTerminateHandler);
        class_replaceMethod(
            delegateClass,
            Selector("applicationShouldTerminate:"),
            implementation,
            "q@:@");
        Log($"Installed applicationShouldTerminate: on delegate class {GetClassName(delegateClass)}");

        // NSWorkspace posts this notification before logout, restart, or shut
        // down. Remember it so those system requests are not mistaken for an
        // ordinary Dock/Finder Quit request.
        var powerOffImplementation = Marshal.GetFunctionPointerForDelegate(workspaceWillPowerOffHandler);
        class_replaceMethod(
            delegateClass,
            Selector("screenTrackerWorkspaceWillPowerOff:"),
            powerOffImplementation,
            "v@:@");

        var workspaceClass = objc_getClass("NSWorkspace");
        var workspace = SendIntPtr(workspaceClass, "sharedWorkspace");
        var notificationCenter = SendIntPtr(workspace, "notificationCenter");
        var notificationName = GetExportedObject(
            "/System/Library/Frameworks/AppKit.framework/AppKit",
            "NSWorkspaceWillPowerOffNotification");

        if (notificationCenter != IntPtr.Zero && notificationName != IntPtr.Zero)
        {
            SendVoidObserverSelectorNameObject(
                notificationCenter,
                "addObserver:selector:name:object:",
                applicationDelegate,
                Selector("screenTrackerWorkspaceWillPowerOff:"),
                notificationName,
                IntPtr.Zero);
        }

        protectedApplicationDelegateClass = delegateClass;
    }

    private static void InstallApplicationTerminateProtection(IntPtr application)
    {
        var applicationClass = object_getClass(application);
        if (applicationClass == IntPtr.Zero || applicationClass == protectedApplicationClass)
            return;

        // Some Catalyst builds use a private NSApplication subclass and do not
        // consistently route Dock > Quit through applicationShouldTerminate:.
        // Guard terminate: on the real runtime class receiving the Dock action.
        var originalMethod = class_getInstanceMethod(applicationClass, Selector("terminate:"));
        originalTerminateImplementation = originalMethod == IntPtr.Zero
            ? IntPtr.Zero
            : method_getImplementation(originalMethod);

        var implementation = Marshal.GetFunctionPointerForDelegate(applicationTerminateHandler);
        class_replaceMethod(
            applicationClass,
            Selector("terminate:"),
            implementation,
            "v@:@");

        Log($"Installed terminate: guard on application class {GetClassName(applicationClass)}");
        protectedApplicationClass = applicationClass;
    }

    [MonoPInvokeCallback(typeof(ApplicationTerminateDelegate))]
    private static void ApplicationTerminate(
        IntPtr self,
        IntPtr selector,
        IntPtr sender)
    {
        Log($"NSApplication terminate: intercepted. systemTerminationRequested={systemTerminationRequested}");

        if (!systemTerminationRequested)
            return;

        if (originalTerminateImplementation == IntPtr.Zero)
            return;

        var originalTerminate = Marshal.GetDelegateForFunctionPointer<ApplicationTerminateDelegate>(
            originalTerminateImplementation);
        originalTerminate(self, selector, sender);
    }

    [MonoPInvokeCallback(typeof(ApplicationShouldTerminateDelegate))]
    private static long ApplicationShouldTerminate(
        IntPtr self,
        IntPtr selector,
        IntPtr sender)
    {
        Log($"applicationShouldTerminate: intercepted. systemTerminationRequested={systemTerminationRequested}");

        // NSTerminateNow for logout/restart/shutdown; NSTerminateCancel keeps
        // the existing protection against ordinary application Quit requests.
        return systemTerminationRequested ? 1 : 0;
    }

    [MonoPInvokeCallback(typeof(WorkspaceWillPowerOffDelegate))]
    private static void WorkspaceWillPowerOff(
        IntPtr self,
        IntPtr selector,
        IntPtr notification)
    {
        Log("NSWorkspaceWillPowerOffNotification received");
        systemTerminationRequested = true;
    }

    private static IntPtr GetExportedObject(string libraryPath, string symbolName)
    {
        if (!NativeLibrary.TryLoad(libraryPath, out var library) ||
            !NativeLibrary.TryGetExport(library, symbolName, out var symbol))
            return IntPtr.Zero;

        return Marshal.ReadIntPtr(symbol);
    }

    private static IntPtr GetSharedApplication()
    {
        var applicationClass = objc_getClass("NSApplication");
        return applicationClass == IntPtr.Zero
            ? IntPtr.Zero
            : SendIntPtr(applicationClass, "sharedApplication");
    }

    private static void ProtectAllNativeWindows(IntPtr application)
    {
        var windows = SendIntPtr(application, "windows");
        var count = SendUInt64(windows, "count");
        if (!loggedWindows || count > 0)
        {
            Log($"Window protection pass. windows={count}; keyWindow={SendIntPtr(application, "keyWindow") != IntPtr.Zero}; mainWindow={SendIntPtr(application, "mainWindow") != IntPtr.Zero}");
            loggedWindows = true;
        }

        for (ulong index = 0; index < count; index++)
            ProtectNativeWindow(SendIntPtrUInt64(windows, "objectAtIndex:", index));

        ProtectNativeWindow(SendIntPtr(application, "keyWindow"));
        ProtectNativeWindow(SendIntPtr(application, "mainWindow"));
    }

    private static void ProtectNativeWindow(IntPtr window)
    {
        if (window == IntPtr.Zero)
            return;

        SetButtonEnabled(window, CloseButton, false);
        SetButtonEnabled(window, ZoomButton, false);

        if (!SendBool(window, "isZoomed"))
            SendVoidIntPtr(window, "zoom:", IntPtr.Zero);
    }

    private static void SetButtonEnabled(IntPtr window, long buttonType, bool enabled)
    {
        var button = SendIntPtrLong(window, "standardWindowButton:", buttonType);
        if (button != IntPtr.Zero)
        {
            SendVoidBool(button, "setEnabled:", enabled);
            Log($"Set standardWindowButton {buttonType} enabled={enabled}");
        }
        else
        {
            Log($"standardWindowButton {buttonType} not found");
        }
    }

    private static void DisableQuitItems(IntPtr menu)
    {
        if (menu == IntPtr.Zero)
            return;

        var items = SendIntPtr(menu, "itemArray");
        var count = SendUInt64(items, "count");
        var terminateSelector = sel_registerName("terminate:");

        for (ulong index = 0; index < count; index++)
        {
            var item = SendIntPtrUInt64(items, "objectAtIndex:", index);
            if (item == IntPtr.Zero)
                continue;

            if (SendIntPtr(item, "action") == terminateSelector)
            {
                SendVoidBool(item, "setEnabled:", false);
                SendVoidIntPtr(item, "setAction:", IntPtr.Zero);
                Log("Disabled NSMenuItem with terminate: action");
            }

            DisableQuitItems(SendIntPtr(item, "submenu"));
        }
    }

    private static IntPtr Selector(string name) => sel_registerName(name);

    internal static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(
                LogPath,
                $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never interfere with startup or tracking.
        }
    }

    private static string GetClassName(IntPtr targetClass)
    {
        if (targetClass == IntPtr.Zero)
            return "(null)";

        var name = class_getName(targetClass);
        return name == IntPtr.Zero ? "(unknown)" : Marshal.PtrToStringAnsi(name) ?? "(unknown)";
    }

    private static IntPtr SendIntPtr(IntPtr receiver, string selector) =>
        receiver == IntPtr.Zero ? IntPtr.Zero : objc_msgSend(receiver, Selector(selector));

    private static bool SendBool(IntPtr receiver, string selector) =>
        receiver != IntPtr.Zero && objc_msgSend_bool(receiver, Selector(selector));

    private static ulong SendUInt64(IntPtr receiver, string selector) =>
        receiver == IntPtr.Zero ? 0 : objc_msgSend_ulong(receiver, Selector(selector));

    private static long SendLong(IntPtr receiver, string selector) =>
        receiver == IntPtr.Zero ? 0 : objc_msgSend_signed_long(receiver, Selector(selector));

    private static bool SendBoolIntPtr(IntPtr receiver, string selector, IntPtr value) =>
        receiver != IntPtr.Zero && objc_msgSend_bool_IntPtr(receiver, Selector(selector), value);

    private static IntPtr SendIntPtrLong(IntPtr receiver, string selector, long value) =>
        objc_msgSend_long(receiver, Selector(selector), value);

    private static IntPtr SendIntPtrUInt64(IntPtr receiver, string selector, ulong value) =>
        objc_msgSend_ulong_arg(receiver, Selector(selector), value);

    private static void SendVoidBool(IntPtr receiver, string selector, bool value) =>
        objc_msgSend_void_bool(receiver, Selector(selector), value);

    private static void SendVoidIntPtr(IntPtr receiver, string selector, IntPtr value) =>
        objc_msgSend_void_IntPtr(receiver, Selector(selector), value);

    private static void SendVoidObserverSelectorNameObject(
        IntPtr receiver,
        string selector,
        IntPtr observer,
        IntPtr callbackSelector,
        IntPtr name,
        IntPtr value) =>
        objc_msgSend_void_observer_selector_name_object(
            receiver,
            Selector(selector),
            observer,
            callbackSelector,
            name,
            value);

    [DllImport(ObjectiveCLibrary)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(ObjectiveCLibrary)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(ObjectiveCLibrary)]
    private static extern IntPtr object_getClass(IntPtr value);

    [DllImport(ObjectiveCLibrary)]
    private static extern IntPtr class_replaceMethod(
        IntPtr targetClass,
        IntPtr selector,
        IntPtr implementation,
        string types);

    [DllImport(ObjectiveCLibrary)]
    private static extern IntPtr class_getInstanceMethod(IntPtr targetClass, IntPtr selector);

    [DllImport(ObjectiveCLibrary)]
    private static extern IntPtr method_getImplementation(IntPtr method);

    [DllImport(ObjectiveCLibrary)]
    private static extern IntPtr class_getName(IntPtr targetClass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ApplicationTerminateDelegate(
        IntPtr self,
        IntPtr selector,
        IntPtr sender);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate long ApplicationShouldTerminateDelegate(
        IntPtr self,
        IntPtr selector,
        IntPtr sender);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void WorkspaceWillPowerOffDelegate(
        IntPtr self,
        IntPtr selector,
        IntPtr notification);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool objc_msgSend_bool(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern ulong objc_msgSend_ulong(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern long objc_msgSend_signed_long(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool objc_msgSend_bool_IntPtr(
        IntPtr receiver,
        IntPtr selector,
        IntPtr value);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_long(IntPtr receiver, IntPtr selector, long value);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_ulong_arg(IntPtr receiver, IntPtr selector, ulong value);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_bool(
        IntPtr receiver,
        IntPtr selector,
        [MarshalAs(UnmanagedType.I1)] bool value);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_IntPtr(
        IntPtr receiver,
        IntPtr selector,
        IntPtr value);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_observer_selector_name_object(
        IntPtr receiver,
        IntPtr selector,
        IntPtr observer,
        IntPtr callbackSelector,
        IntPtr name,
        IntPtr value);

}
#endif
