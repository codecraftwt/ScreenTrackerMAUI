using Microsoft.IdentityModel.Tokens;
using Microsoft.Maui.Controls;
using ScreenTracker1.Services;

namespace ScreenTracker1;

public partial class App : Application
{


    //public static string BaseUrl { get; set; } = "http://10.0.3.215:85";

    //public static string ImgURL { get; set; } = $"{App.BaseUrl}/wwwroot/uploads/";

    public static string BaseUrl { get; set; } = "http://10.0.3.215:89";

    //public static string BaseUrl { get; set; } = "http://localhost:5011";

    public static string ImgURL { get; set; } = $"{BaseUrl}/uploads/";


    public static bool first { get; set; } = true;
    public static string URL { get; set; } = $"{App.BaseUrl}/api/";
    //public static int UserID { get; set; } = 0;
    public static int SelectedUserId { get; set; } = 0;
    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        var tracker = serviceProvider.GetService<AppUsageTracker>();


        MainPage = new MainPage();
    }
}

