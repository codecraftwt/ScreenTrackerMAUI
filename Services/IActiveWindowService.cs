namespace ScreenTracker1.Services
{
    /// <summary>
    /// Cross-platform interface for retrieving the currently active (foreground)
    /// application name and its active window title.
    /// 
    /// On Windows, implemented via P/Invoke (GetForegroundWindow, GetWindowText).
    /// On macOS (Mac Catalyst), implemented via CoreGraphics CGWindowListCopyWindowInfo
    /// with AppleScript fallback.
    /// </summary>
    public interface IActiveWindowService
    {
        /// <summary>
        /// Returns the name of the currently active (frontmost) application.
        /// Example: "Google Chrome", "Visual Studio Code", "Finder".
        /// Returns "Unknown" if detection fails.
        /// </summary>
        string GetActiveAppName();

        /// <summary>
        /// Returns the title of the currently active (frontmost) window.
        /// Example: "My Document - Microsoft Word", "index.html — Visual Studio Code".
        /// Returns empty string if detection fails.
        /// </summary>
        string GetActiveWindowTitle();

        /// <summary>
        /// Fires when the active application and/or window title changes.
        /// Parameters: (appName, windowTitle)
        /// </summary>
        event Action<string, string>? OnActiveWindowChanged;

        /// <summary>
        /// Starts monitoring the active window. Must be called before polling begins.
        /// </summary>
        void Start();

        /// <summary>
        /// Returns diagnostic information about the current detection state.
        /// Used for live debugging in the UI.
        /// </summary>
        string GetDiagnosticInfo();
    }
}
