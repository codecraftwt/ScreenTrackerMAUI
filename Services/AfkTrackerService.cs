using ScreenTracker1.DTOS;
using System;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
#if MACCATALYST
using ScreenTracker1.Platforms.MacCatalyst;
#endif

namespace ScreenTracker1.Services
{
    public class AfkTrackerService : IAfkDetectorService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private int _userId;
        private string _startMode;
        private System.Timers.Timer _timer;
        private DateTime? _afkStartTime;
        private bool _isAfk;
        private DateTime _lastCheckTime = DateTime.UtcNow;
        private double _lastObservedIdleSeconds;
        private readonly SemaphoreSlim _checkGate = new(1, 1);
        private const int AfkThresholdSeconds = 1200;

        public bool IsRunning => _timer != null;

#if WINDOWS
        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
#endif

        public AfkTrackerService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri($"{App.URL}");
        }

        public void Start(int userID, string startMode)
        {
            _userId = userID;
            _startMode = startMode;
            _lastCheckTime = DateTime.UtcNow;
            _lastObservedIdleSeconds = GetIdleTimeInSeconds();
            _timer = new System.Timers.Timer(10000);
            _timer.Elapsed += CheckAfkStatus;
            _timer.AutoReset = true;
            _timer.Start();
        }

        private async void CheckAfkStatus(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (!await _checkGate.WaitAsync(0))
                return;

            try
            {
            var now = DateTime.UtcNow;
            var previousCheckTime = _lastCheckTime;
            var checkGap = now - _lastCheckTime;
            _lastCheckTime = now;

            var idleTime = GetIdleTimeInSeconds();

#if MACCATALYST
            // A System.Timers.Timer is suspended while macOS sleeps. On wake, keyboard
            // or mouse input may reset the native idle counter before this callback.
            // Preserve the idle duration seen before sleep and add the suspended gap.
            if (checkGap > TimeSpan.FromSeconds(30))
            {
                double combinedIdleSeconds = _lastObservedIdleSeconds + checkGap.TotalSeconds;
                DateTime combinedAfkStart = _isAfk && _afkStartTime.HasValue
                    ? _afkStartTime.Value
                    : previousCheckTime.AddSeconds(-_lastObservedIdleSeconds);

                Console.WriteLine(
                    $"[AFK] macOS sleep detected. Before sleep: {_lastObservedIdleSeconds / 60:F1}m, " +
                    $"sleep gap: {checkGap.TotalMinutes:F1}m, combined: {combinedIdleSeconds / 60:F1}m");

                if (combinedIdleSeconds >= AfkThresholdSeconds)
                {
                    if (idleTime >= AfkThresholdSeconds)
                    {
                        // Still no input after wake: keep one AFK session open. The
                        // existing logic below will close it when input resumes.
                        _afkStartTime = combinedAfkStart;
                        _isAfk = true;
                    }
                    else
                    {
                        // Unlock input already reset the idle counter, so persist the
                        // completed idle + sleep interval immediately.
                        var afkLog = new AfkLogDto
                        {
                            UserId = _userId,
                            AfkStartTime = combinedAfkStart,
                            AfkEndTime = now,
                            Duration = now - combinedAfkStart,
                            StartMode = _startMode
                        };
                        if (await PostAfkLogAsync(afkLog))
                        {
                            _isAfk = false;
                            _afkStartTime = null;
                        }
                    }
                }

                _lastObservedIdleSeconds = idleTime;
                return;
            }
#endif

            if (idleTime >= AfkThresholdSeconds && !_isAfk)
            {
                _afkStartTime = DateTime.UtcNow.AddSeconds(-idleTime);
                _isAfk = true;
                Console.WriteLine($"[AFK Start] {_afkStartTime}");
            }
            else if (idleTime < AfkThresholdSeconds && _isAfk)
            {
                var afkEndTime = now;
                var duration = afkEndTime - _afkStartTime.Value;

                var afkLog = new AfkLogDto
                {
                    UserId = _userId,
                    AfkStartTime = _afkStartTime.Value,
                    AfkEndTime = afkEndTime,
                    Duration = duration,
                    StartMode = _startMode
                };

                if (await PostAfkLogAsync(afkLog))
                {
                    _isAfk = false;
                    _afkStartTime = null;
                    Console.WriteLine($"[AFK End] {afkEndTime} | Duration: {duration}");
                }
            }

            _lastObservedIdleSeconds = idleTime;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AFK Check Error] {ex.Message}");
            }
            finally
            {
                _checkGate.Release();
            }
        }

        private int GetIdleTimeInSeconds()
        {
#if WINDOWS
            LASTINPUTINFO lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
            if (!GetLastInputInfo(ref lastInputInfo)) return 0;

            uint idleTime = ((uint)Environment.TickCount - lastInputInfo.dwTime);
            return (int)(idleTime / 1000);
#elif MACCATALYST
            return (int)MacIdleTimeService.GetIdleTimeInSeconds();
#else
            return 0;
#endif
        }

        private async Task<bool> PostAfkLogAsync(AfkLogDto afkLog)
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    // Read the token for every retry because wake recovery may
                    // refresh it while this AFK log is being submitted.
                    var token = Preferences.Get("authToken", null);
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        Console.WriteLine($"[AFK API Error] No auth token (attempt {attempt}/3).");
                    }
                    else
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Post, "AfkLogs");
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                        request.Content = JsonContent.Create(afkLog);

                        using var response = await _httpClient.SendAsync(request);
                        if (response.IsSuccessStatusCode)
                            return true;

                        string responseBody = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"[AFK API Error] Attempt {attempt}/3, {response.StatusCode}: {responseBody}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AFK Service Error] Attempt {attempt}/3: {ex.Message}");
                }

                if (attempt < 3)
                    await Task.Delay(TimeSpan.FromSeconds(2));
            }

            return false;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }

        public void Stop()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
                _timer = null;

                if (_isAfk && _afkStartTime.HasValue)
                {
                    var afkEndTime = DateTime.UtcNow;
                    var duration = afkEndTime - _afkStartTime.Value;
                    var afkLog = new AfkLogDto
                    {
                        UserId = _userId,
                        AfkStartTime = _afkStartTime.Value,
                        AfkEndTime = afkEndTime,
                        Duration = duration,
                        StartMode = _startMode
                    };
                    Task.Run(() => PostAfkLogAsync(afkLog));
                }
            }
        }
    }
}
