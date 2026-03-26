using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace ScreenTracker1.Services
{
    public interface IWindowsThemeService
    {
        bool IsSystemDarkTheme();
    }

    public class WindowsThemeService : IWindowsThemeService
    {
        public bool IsSystemDarkTheme()
        {
#if WINDOWS
            try
            {
                var uiSettings = new Windows.UI.ViewManagement.UISettings();
                var color = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
                return (color.R + color.G + color.B) < 382;
            }
            catch
            {
                return Application.Current.RequestedTheme == AppTheme.Dark;
            }
#else
            // For non-Windows platforms, use MAUI's detection
            return Application.Current.RequestedTheme == AppTheme.Dark;
#endif
        }
    }
}