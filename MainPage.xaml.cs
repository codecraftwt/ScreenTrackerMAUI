using Microsoft.Maui.Controls;
using ScreenTracker1.Services;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ScreenTracker1
{
    public partial class MainPage : ContentPage
    {
        private readonly AppUsageTracker _appUsageTrackerService;
        private readonly DesktopAutoCaptureService _desktopAutoCaptureService;
        private readonly AfkTrackerService _afkTrackerService;

        // This variable tracks the UI state
        private bool _isOn;

        public MainPage(AppUsageTracker appUsageTrackerService, DesktopAutoCaptureService desktopAutoCaptureService, AfkTrackerService afkTrackerService)
        {
            InitializeComponent();
            _appUsageTrackerService = appUsageTrackerService;
            _desktopAutoCaptureService = desktopAutoCaptureService;
            _afkTrackerService = afkTrackerService;

            // Immediately start the tracker services when the page is loaded.
            // This is the core logic that makes the tracker start automatically.
            StartAll();

            // Set the UI state to "ON" since the services have been started.
            _isOn = true;
            Preferences.Set("IsOnState", _isOn);

            Debug.WriteLine("Tracker services started automatically on application launch.");
        }

        private async Task OnToggleStateClicked()
        {
            _isOn = !_isOn;
            Preferences.Set("IsOnState", _isOn);
            if (_isOn) StartAll(); else StopAll();
        }

        private void StartAll()
        {
            Debug.WriteLine("Attempting to start all tracker services...");
            try
            {
                _appUsageTrackerService.Start();
                Debug.WriteLine("AppUsageTracker service started.");
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"Error starting AppUsageTracker: {ex.Message}");
            }
            try
            {
                _desktopAutoCaptureService.Start();
                Debug.WriteLine("DesktopAutoCaptureService started.");
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"Error starting DesktopAutoCaptureService: {ex.Message}");
            }
            try
            {
                _afkTrackerService.Start(App.SelectedUserId);
                Debug.WriteLine("AfkTrackerService started.");
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"Error starting AfkTrackerService: {ex.Message}");
            }
        }

        private void StopAll()
        {
            Debug.WriteLine("Attempting to stop all tracker services...");
            try
            {
                _appUsageTrackerService.Stop();
                Debug.WriteLine("AppUsageTracker service stopped.");
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"Error stopping AppUsageTracker: {ex.Message}");
            }
            try
            {
                _desktopAutoCaptureService.StopTimer();
                Debug.WriteLine("DesktopAutoCaptureService stopped.");
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"Error stopping DesktopAutoCaptureService: {ex.Message}");
            }
            try
            {
                _afkTrackerService.Stop();
                Debug.WriteLine("AfkTrackerService stopped.");
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"Error stopping AfkTrackerService: {ex.Message}");
            }
        }
    }
}