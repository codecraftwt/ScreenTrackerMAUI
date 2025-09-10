using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;

namespace ScreenTracker1.Services
{
    public interface INavigationService
    {
        void NavigateTo(string url);
    }

    public class NavigationService : INavigationService
    {
        private readonly NavigationManager _navigationManager;

        public NavigationService(NavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
        }

        public void NavigateTo(string url)
        {
           
            _navigationManager.NavigateTo(url, forceLoad: true);
        }
    }
}
