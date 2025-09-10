using ScreenTracker1.DTOS;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ScreenTracker1.Services
{
    public class AfkTrackerService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private int _userId;
        private System.Timers.Timer _timer;
        private DateTime? _afkStartTime;
        private bool _isAfk;
        private const int AfkThresholdSeconds = 60;

        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        public AfkTrackerService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri($"{App.URL}");
        }


        public AfkTrackerService(HttpClient httpClient, string token)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri($"{App.URL}");

        }

        public void Start(int userID)
        {
            _userId = userID;
            _timer = new System.Timers.Timer(10000);
            _timer.Elapsed += CheckAfkStatus;
            _timer.AutoReset = true;
            _timer.Start();
        }

        private void CheckAfkStatus(object sender, System.Timers.ElapsedEventArgs e)
        {
            var idleTime = GetIdleTimeInSeconds();

            if (idleTime >= AfkThresholdSeconds && !_isAfk)
            {
             
                _afkStartTime = DateTime.UtcNow;
                _isAfk = true;
                Console.WriteLine($"[AFK Start] {_afkStartTime}");
            }
            else if (idleTime < AfkThresholdSeconds && _isAfk)
            {
                var afkEndTime = DateTime.UtcNow;
                var duration = afkEndTime - _afkStartTime.Value;

                var afkLog = new AfkLogDto
                {
                    UserId = _userId,
                    AfkStartTime = _afkStartTime.Value,
                    AfkEndTime = afkEndTime,
                    Duration = duration
                };

                PostAfkLogAsync(afkLog);

                _isAfk = false;
                _afkStartTime = null;
                Console.WriteLine($"[AFK End] {afkEndTime} | Duration: {duration}");
            }
        }

        private int GetIdleTimeInSeconds()
        {
            LASTINPUTINFO lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
            if (!GetLastInputInfo(ref lastInputInfo)) return 0;

            uint idleTime = ((uint)Environment.TickCount - lastInputInfo.dwTime);
            return (int)(idleTime / 1000);
        }

        private async Task PostAfkLogAsync(AfkLogDto afkLog)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("AfkLogs", afkLog);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[API Error] {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AFK Service Error] {ex.Message}");
            }
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
                        Duration = duration
                    };
                    PostAfkLogAsync(afkLog);
                }
            }
        }

    }
}
