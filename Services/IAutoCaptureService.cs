namespace ScreenTracker1.Services
{
    public interface IAutoCaptureService
    {
        bool IsRunning { get; }
        void Start(string startMode);
        void StopTimer();
    }
}
