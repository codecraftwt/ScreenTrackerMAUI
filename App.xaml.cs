using Microsoft.IdentityModel.Tokens;
using Microsoft.Maui.Controls;
using ScreenTracker1.Services;
#if MACCATALYST
using ScreenTracker1.Platforms.MacCatalyst;
#endif

namespace ScreenTracker1;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    //public static string BaseUrl { get; set; } = "http://localhost:5011";
    //http://10.0.3.109:99/api/user/allUsers
    //public static string BaseUrl { get; set; } = "http://10.0.3.109:99";
    //http://10.0.3.64:99/api/user/allUsers
    //http://10.0.3.68:99/api/user/allUsers
    //http://screentracker.walstargroup.org/api/user/allUsersa
    //public static string BaseUrl { get; set; } = "http://screentracker.walstargroup.org";

    public static string BaseUrl { get; set; } = "http://10.0.3.55:90";


    public static string ImgURL { get; set; } = $"{BaseUrl}/uploads/screenshots/";


    public static bool first { get; set; } = true;
    public static bool DailyTrackerStartedDuringAutoLogin { get; set; }
    public static string URL { get; set; } = $"{App.BaseUrl}/api/";
  
   
    public static int SelectedUserId { get; set; } = 0;
    public static string SelectedUsername { get; set; } = string.Empty;
    public static string SelectedAdminUsername { get; set; } = string.Empty;
    public static string selectedUsageType { get; set; } = "all";

    /// <summary>
    /// Converts an image URL (filename, relative path, or full URL) to a fully qualified URL.
    /// Handles three cases:
    /// 1. Already a full URL (http:// or https://) → returned as-is
    /// 2. Relative path with "uploads/" prefix → prefixed with BaseUrl
    /// 3. Plain filename or other path → prefixed with ImgURL
    /// </summary>
    public static string GetFullImageUrl(string? imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl))
            return string.Empty;

        // data: URIs (base64 embedded images) should be returned as-is
        if (imageUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return imageUrl;

        // file: URIs (temp files for macOS compatibility) should be returned as-is
        if (imageUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            return imageUrl;

        if (imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return imageUrl;

        // Normalize: replace Windows backslashes with forward slashes for macOS compatibility
        string cleanUrl = imageUrl.TrimStart('/').Replace("\\", "/");

        if (cleanUrl.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            return $"{BaseUrl}/{cleanUrl}";

        var result = $"{ImgURL}{cleanUrl}";

        // Log every URL generation for debugging (will be cleaned up after fix is verified)
        Console.WriteLine($"[GetFullImageUrl] Input: '{imageUrl}', Output: '{result}'");

        return result;
    }
    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        MainPage = serviceProvider.GetRequiredService<MainPage>();
    }

    protected override void OnResume()
    {
        base.OnResume();
#if MACCATALYST
        HandleMacResume();
#endif
    }

#if MACCATALYST
    private void HandleMacResume()
    {
        try
        {
            double idleSeconds = MacIdleTimeService.GetSleepAwareIdleTimeInSeconds();
            Console.WriteLine($"[App] Mac resumed. AFK duration: {TimeSpan.FromSeconds(idleSeconds):hh\\:mm\\:ss}");

            Task.Run(async () =>
            {
                try
                {
                    var userService = _serviceProvider.GetRequiredService<UserService>();
                    bool recovered = await userService.TryHeartbeatRecoveryAsync();
                    if (recovered)
                    {
                        Console.WriteLine("[App] Session recovered after Mac wake.");
                    }
                    else
                    {
                        Console.WriteLine("[App] Session recovery after Mac wake deferred to next API call.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[App] Resume recovery error: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[App] HandleMacResume error: {ex.Message}");
        }
    }
#endif
}
