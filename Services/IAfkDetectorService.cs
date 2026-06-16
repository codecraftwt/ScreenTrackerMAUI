namespace ScreenTracker1.Services
{
    public interface IAfkDetectorService
    {
        void Start(int userId, string startMode);
        void Stop();
    }
}
