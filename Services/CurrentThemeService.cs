using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenTracker1.Services
{
    public class CurrentThemeService
    {
        // This property tracks the theme state
        public bool IsDarkTheme { get; private set; } = false;

        // Event used to notify components of a theme change
        public event Action? OnChange;

        public void SetTheme(bool isDark)
        {
            IsDarkTheme = isDark;
            NotifyStateChanged();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}

