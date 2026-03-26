namespace ScreenTracker1.Services
{
    public static class Logger
    {
        // ... (LogDirectory setup remains the same, but ensure Path is available) ...
        private static readonly string LogDirectory = FileSystem.AppDataDirectory;
        private const string LogFilePrefix = "ScreenshotLog_";
        private const string LogFileExtension = ".txt";

        public static void Log(string message)
        {
            try
            {
                string logFileName = $"{LogFilePrefix}{DateTime.Now:yyyyMMdd}{LogFileExtension}";
                // NOTE: This requires 'using System.IO;' at the top
                string logFilePath = Path.Combine(LogDirectory, logFileName);

                // TEMPORARY: Keep this line until you confirm the path, then remove it.
                Console.WriteLine($"LOGGER PATH: {logFilePath}");

                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }

                string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}{Environment.NewLine}";
                File.AppendAllText(logFilePath, logEntry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FATAL LOGGING ERROR: {ex.Message}");
            }
        }
    }
}
