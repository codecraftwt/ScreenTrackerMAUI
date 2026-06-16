namespace ScreenTracker1.Services
{
    public interface IActiveWindowTrackerService
    {
        void Start(string startMode);
        void Stop();
    }
}
