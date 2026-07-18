using Foundation;
using Microsoft.AspNetCore.Components.WebView.Maui;
using WebKit;

namespace ScreenTracker1.Platforms.MacCatalyst
{
    public class CustomBlazorWebViewHandler : BlazorWebViewHandler
    {
        protected override void ConnectHandler(WKWebView platformView)
        {
            base.ConnectHandler(platformView);

            var config = platformView.Configuration;
            var preferences = config.Preferences;

            if (preferences != null)
            {
                // Allow file:// origin to access file:// resources
                preferences.SetValueForKey(
                    NSObject.FromObject(true),
                    new NSString("allowUniversalAccessFromFileURLs"));
                preferences.SetValueForKey(
                    NSObject.FromObject(true),
                    new NSString("allowFileAccessFromFileURLs"));

                // Allow http:// images to load inside the file:// Blazor WebView
                // (equivalent to disabling mixed content blocking)
                preferences.SetValueForKey(
                    NSObject.FromObject(true),
                    new NSString("allowRunningInsecureContent"));
            }

            // Inject a script that runs before page content to allow HTTP image loads
            var script = new WKUserScript(
                new NSString(
                    "Object.defineProperty(window, 'isSecureContext', { get: () => true });"
                ),
                WKUserScriptInjectionTime.AtDocumentStart,
                true
            );
            config.UserContentController.AddUserScript(script);
        }
    }
}
