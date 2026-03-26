using Microsoft.IdentityModel.Tokens;
using Microsoft.Maui.Controls;
using ScreenTracker1.Services;

namespace ScreenTracker1;

public partial class App : Application
{

    public static string BaseUrl { get; set; } = "http://localhost:5011";
    //http://10.0.3.64:99/api/user/allUsers
    //public static string BaseUrl { get; set; } = "http://10.0.3.64:99";
    public static string ImgURL { get; set; } = $"{BaseUrl}/uploads/";


    public static bool first { get; set; } = true;
    public static string URL { get; set; } = $"{App.BaseUrl}/api/";
   
    public static int SelectedUserId { get; set; } = 0;
    public static string SelectedUsername { get; set; } = string.Empty;
    public static string SelectedAdminUsername { get; set; } = string.Empty;
    public static string selectedUsageType { get; set; } = "all";
    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        var tracker = serviceProvider.GetService<AppUsageTracker>();
        MainPage = serviceProvider.GetRequiredService<MainPage>();
    }
}

