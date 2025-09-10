using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using MudBlazor.Services;
using Radzen;
using ScreenTracker1.Platforms.Windows;
using ScreenTracker1.Services;
using System.Net.Http;

namespace ScreenTracker1
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
                });
            builder.Services.AddRadzenComponents();
            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
#endif

         
            builder.Services.AddMudServices();

            builder.Services.AddSingleton<UserService>();
            builder.Services.AddScoped<LoginService>();
            builder.Services.AddScoped<RegisterService>();
            builder.Services.AddScoped<AfkTrackerService>();
            builder.Services.AddScoped<AppStateService>();
            builder.Services.AddSingleton<AppUsageTracker>();

#if WINDOWS
            builder.Services.AddSingleton<DesktopScreenshotService>();
            builder.Services.AddSingleton<DesktopAutoCaptureService>();
            builder.Services.AddSingleton<KeyboardMouseService>();
            builder.Services.AddSingleton<ImageService>();
            builder.Services.AddSingleton<SystemThemeService>();
        
#endif

            // HttpClient
            builder.Services.AddScoped(sp => new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
