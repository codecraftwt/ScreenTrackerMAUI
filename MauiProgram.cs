using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using MudBlazor.Services;
using Radzen;
using ScreenTracker1.Services;
using System.Net.Http;
using Microsoft.Maui.LifecycleEvents;
using System;
using Microsoft.Extensions.DependencyInjection;
using ScreenTracker1.Platforms.Windows;

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
                })
                .ConfigureLifecycleEvents(events =>
                {
#if WINDOWS
                    events.AddWindows(windows => windows.OnClosed((window, args) =>
                    {
                        var serviceProvider = builder.Services.BuildServiceProvider();
                        try { serviceProvider.GetRequiredService<AppUsageTracker>().Stop(); } catch { }
                        try { serviceProvider.GetRequiredService<DesktopAutoCaptureService>().StopTimer(); } catch { }
                        try { serviceProvider.GetRequiredService<AfkTrackerService>().Stop(); } catch { }
                    }));
#endif
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
            builder.Services.AddSingleton<AfkTrackerService>();
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

            // This is the crucial line to register MainPage as a service
            builder.Services.AddSingleton<MainPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
