using Foundation;
using ScreenTracker1.Platforms.MacCatalyst;
using UIKit;

namespace ScreenTracker1
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
        {
            var launched = base.FinishedLaunching(application, launchOptions);
            MacStartupAndWindowProtection.Initialize();
            return launched;
        }

        [Export("applicationShouldTerminate:")]
        public nint ApplicationShouldTerminate(IntPtr sender)
        {
            MacStartupAndWindowProtection.Log("AppDelegate applicationShouldTerminate: cancel");
            return 0;
        }

        [Export("applicationShouldTerminateAfterLastWindowClosed:")]
        public bool ApplicationShouldTerminateAfterLastWindowClosed(IntPtr sender)
        {
            MacStartupAndWindowProtection.Log("AppDelegate applicationShouldTerminateAfterLastWindowClosed: false");
            return false;
        }
    }
}
