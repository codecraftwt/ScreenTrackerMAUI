using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenTracker1.Services
{
    public class SystemThemeService
    {
        public bool IsSystemDarkTheme()
        {
            // This uses MAUI's built-in theme detection
            return Application.Current.RequestedTheme == AppTheme.Dark;
        }
    }
}