namespace ScreenTracker1.Services
{
    public interface IAutoCaptureService
    {
        void Start(string startMode);
        void StopTimer();
    }
}
