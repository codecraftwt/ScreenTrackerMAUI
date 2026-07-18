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
#if WINDOWS
using ScreenTracker1.Platforms.Windows;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using WinRT.Interop;
#endif

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
                 events.AddWindows(windows =>
                 {
                     windows.OnWindowCreated(window =>
                     {
                         window.ExtendsContentIntoTitleBar = false;
                         IntPtr nativeWindowHandle = WindowNative.GetWindowHandle(window);
                         WindowId winuiAppWindowId = Win32Interop.GetWindowIdFromWindow(nativeWindowHandle);
                         AppWindow winuiAppWindow = AppWindow.GetFromWindowId(winuiAppWindowId);

                         if (winuiAppWindow.Presenter is OverlappedPresenter presenter)
                         {
                             presenter.Maximize();
                         }
                        
                         new WindowInterop(nativeWindowHandle).DisableCloseButton();
                         NativeThemeService.SetWindowHandle(nativeWindowHandle);
                         winuiAppWindow.Closing += (sender, args) =>
                         {
                             args.Cancel = true;
                         };
                     });

                     windows.OnClosed((window, args) =>
                     {
                         var serviceProvider = builder.Services.BuildServiceProvider();
                         try { serviceProvider.GetRequiredService<AppUsageTracker>().Stop(); } catch { }
                         try { serviceProvider.GetRequiredService<DesktopAutoCaptureService>().StopTimer(); } catch { }
                         try { serviceProvider.GetRequiredService<AfkTrackerService>().Stop(); } catch { }
                     });
                 });
#endif
             });

            builder.Services.AddRadzenComponents();
            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
#endif

            builder.Services.AddMudServices();
            builder.Services.AddSingleton<AfkTrackerService>();
            builder.Services.AddSingleton<AppUsageTracker>();
            builder.Services.AddSingleton<NativeThemeService>();
            builder.Services.AddSingleton<UserService>();
            builder.Services.AddScoped<LoginService>();
            builder.Services.AddScoped<RegisterService>();
            builder.Services.AddScoped<AppStateService>();
            builder.Services.AddSingleton<UserStateService>();
            builder.Services.AddScoped<CurrentThemeService>();

#if WINDOWS
            builder.Services.AddSingleton<IScreenshotService, Platforms.Windows.DesktopScreenshotService>();
#elif MACCATALYST
            builder.Services.AddSingleton<IScreenshotService, Platforms.MacCatalyst.DesktopScreenshotService>();
#endif
            builder.Services.AddSingleton<DesktopAutoCaptureService>();
            builder.Services.AddSingleton<IAutoCaptureService>(sp => sp.GetRequiredService<DesktopAutoCaptureService>());
            builder.Services.AddSingleton<KeyboardMouseService>();
            builder.Services.AddSingleton<ImageService>();
            builder.Services.AddSingleton<SystemThemeService>();
            builder.Services.AddSingleton<SharedStateService>();
            builder.Services.AddSingleton<UserSelectionStateService>();
            builder.Services.AddSingleton<IWebAuthenticator>(WebAuthenticator.Default);

            builder.Services.AddScoped(sp => new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            });

            builder.Services.AddSingleton<MainPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}