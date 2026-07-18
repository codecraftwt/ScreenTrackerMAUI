using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
#if MACCATALYST
using Foundation;
using ScreenTracker1.Platforms.MacCatalyst;
#endif

namespace ScreenTracker1.Services
{
    public class SessionKeepAliveService : IDisposable
    {
        private readonly UserService _userService;
        private readonly HttpClient _httpClient;
        private Timer _heartbeatTimer;
        private Timer _tokenRefreshTimer;
        private Timer _scheduledRefreshTimer;
        private bool _isRunning;
        private string _cachedToken;
        private int _failedHeartbeatCount;
        private DateTime _lastTimerTick = DateTime.UtcNow;
        public static bool WasSleeping { get; private set; }
#if MACCATALYST
        private NSObject _sleepObserver;
        private NSObject _wakeObserver;
#endif

        public SessionKeepAliveService(UserService userService, HttpClient httpClient)
        {
            _userService = userService;
            _httpClient = httpClient;
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _failedHeartbeatCount = 0;
            _cachedToken = null;

            _heartbeatTimer = new Timer(async _ => await DoHeartbeatAsync(), null,
                TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(3));

            _tokenRefreshTimer = new Timer(async _ => await CheckAndRefreshTokenAsync(), null,
                TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));

            _scheduledRefreshTimer = new Timer(async _ =>
            {
                Console.WriteLine("[KeepAlive] Scheduled refresh triggered.");
                var token = GetToken();
                if (!string.IsNullOrEmpty(token))
                {
                    bool refreshed = await RefreshTokenAsync(token);
                    if (refreshed)
                    {
                        _failedHeartbeatCount = 0;
                        ScheduleNextRefresh();
                    }
                }
            }, null, Timeout.Infinite, Timeout.Infinite);

            ScheduleNextRefresh();

            Console.WriteLine("[KeepAlive] Started (scheduled, gap: 30s, heartbeat: 3min)");

#if MACCATALYST
            SetupMacSleepWakeListeners();
#endif
        }

        public void Stop()
        {
#if MACCATALYST
            TeardownMacSleepWakeListeners();
#endif
            _isRunning = false;
            _heartbeatTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _tokenRefreshTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _scheduledRefreshTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public async Task<bool> CheckAndRefreshNowAsync()
        {
            var token = GetToken();
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("[KeepAlive] CheckAndRefreshNow: no token found.");
                return false;
            }

            if (!IsTokenExpiringSoon(token, TimeSpan.FromMinutes(1)))
            {
                Console.WriteLine("[KeepAlive] CheckAndRefreshNow: token still valid.");
                return true;
            }

            Console.WriteLine("[KeepAlive] CheckAndRefreshNow: token expired/expiring soon, refreshing immediately...");
            return await RefreshTokenAsync(token);
        }

        private async Task DoHeartbeatAsync()
        {
            try
            {
                var token = GetToken();
                if (string.IsNullOrEmpty(token)) return;

                var request = new HttpRequestMessage(HttpMethod.Post, $"{App.URL}Auth/heartbeat");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    _failedHeartbeatCount = 0;
                    return;
                }

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _failedHeartbeatCount++;
                    Console.WriteLine($"[KeepAlive] Heartbeat 401 (#{_failedHeartbeatCount})");

                    if (_failedHeartbeatCount >= 3)
                    {
                        Console.WriteLine("[KeepAlive] Too many heartbeat failures. Stopping.");
                        Stop();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KeepAlive] Heartbeat error: {ex.Message}");
            }
        }

        private async Task CheckAndRefreshTokenAsync()
        {
            try
            {
                var now = DateTime.UtcNow;
                var gap = now - _lastTimerTick;
                _lastTimerTick = now;

                var token = GetToken();
                if (string.IsNullOrEmpty(token)) return;

                if (gap > TimeSpan.FromMinutes(3))
                {
                    WasSleeping = true;
                    Console.WriteLine($"[KeepAlive] Detected {gap.TotalMinutes:F0}min gap (Mac slept). Refreshing token now...");
                    bool refreshed = await RefreshTokenAsync(token);
                    if (refreshed)
                    {
                        _failedHeartbeatCount = 0;
                        WasSleeping = false;
                        Console.WriteLine("[KeepAlive] Token refreshed after sleep.");
                    }
                    else
                    {
                        Console.WriteLine("[KeepAlive] Token refresh after sleep failed. Will try heartbeat recovery on next API call.");
                    }
                    return;
                }

                if (!IsTokenExpiringSoon(token, TimeSpan.FromMinutes(10)))
                    return;

                Console.WriteLine("[KeepAlive] Token expiring soon, attempting refresh...");
                bool refreshed2 = await RefreshTokenAsync(token);

                if (refreshed2)
                {
                    _failedHeartbeatCount = 0;
                    Console.WriteLine("[KeepAlive] Token refreshed successfully.");
                }
                else
                {
                    Console.WriteLine("[KeepAlive] Token refresh failed.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KeepAlive] Token check error: {ex.Message}");
            }
        }

        private static bool IsTokenExpiringSoon(string token, TimeSpan threshold)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadToken(token) as JwtSecurityToken;
                if (jwtToken == null) return true;
                bool expiring = jwtToken.ValidTo < DateTime.UtcNow.Add(threshold);
                if (expiring)
                    Console.WriteLine($"[KeepAlive] Token expires at {jwtToken.ValidTo.ToLocalTime()}, refreshing now.");
                return expiring;
            }
            catch
            {
                return true;
            }
        }

        private async Task<bool> RefreshTokenAsync(string currentToken)
        {
            try
            {
                var payload = JsonSerializer.Serialize(new { token = currentToken });
                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{App.URL}auth/refresh-token", content);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[KeepAlive] Refresh returned {response.StatusCode}");
                    return false;
                }

                var body = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                string newToken = null;

                if (doc.RootElement.TryGetProperty("token", out var t))
                    newToken = t.GetString();
                else if (doc.RootElement.TryGetProperty("accessToken", out var at))
                    newToken = at.GetString();
                else if (doc.RootElement.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object)
                {
                    if (d.TryGetProperty("token", out var dt))
                        newToken = dt.GetString();
                }

                if (string.IsNullOrEmpty(newToken))
                {
                    Console.WriteLine("[KeepAlive] Refresh response did not contain a token field.");
                    return false;
                }

                Preferences.Set("authToken", newToken);
                Preferences.Set("auth_token", newToken);
                _cachedToken = newToken;
                ScheduleNextRefresh();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KeepAlive] Refresh exception: {ex.Message}");
                return false;
            }
        }

        private string GetToken()
        {
            if (!string.IsNullOrEmpty(_cachedToken))
                return _cachedToken;
            _cachedToken = Preferences.Get("authToken", null);
            return _cachedToken;
        }

        private void ScheduleNextRefresh()
        {
            var token = GetToken();
            if (string.IsNullOrEmpty(token)) return;

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadToken(token) as JwtSecurityToken;
                if (jwtToken == null || jwtToken.ValidTo == DateTime.MinValue) return;

                var expiresAt = jwtToken.ValidTo;
                var refreshAt = expiresAt.AddMinutes(-5);
                var delay = refreshAt - DateTime.UtcNow;

                if (delay <= TimeSpan.Zero)
                {
                    Console.WriteLine($"[KeepAlive] Token expires at {expiresAt.ToLocalTime():HH:mm:ss}, already past refresh window. Refreshing now.");
                    _ = RefreshTokenAsync(token);
                    return;
                }

                _scheduledRefreshTimer?.Change(delay, Timeout.InfiniteTimeSpan);
                Console.WriteLine($"[KeepAlive] Token expires at {expiresAt.ToLocalTime():HH:mm:ss}, next refresh in {delay.TotalMinutes:F1}min");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KeepAlive] ScheduleNextRefresh error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Stop();
            _heartbeatTimer?.Dispose();
            _tokenRefreshTimer?.Dispose();
            _scheduledRefreshTimer?.Dispose();
        }

#if MACCATALYST
        private void SetupMacSleepWakeListeners()
        {
            try
            {
                _sleepObserver = NSNotificationCenter.DefaultCenter.AddObserver(
                    new NSString("NSWorkspaceWillSleepNotification"),
                    notification =>
                    {
                        Console.WriteLine("[KeepAlive] Mac about to sleep - saving activity and refreshing token...");
                        MacIdleTimeService.SaveActivity();
                        WasSleeping = true;
                        string? token = GetToken();
                        if (!string.IsNullOrEmpty(token))
                            _ = RefreshTokenAsync(token);
                    });

                _wakeObserver = NSNotificationCenter.DefaultCenter.AddObserver(
                    new NSString("NSWorkspaceDidWakeNotification"),
                    async notification =>
                    {
                        Console.WriteLine("[KeepAlive] Mac woke from sleep - recovering session...");
                        WasSleeping = true;
                        await CheckAndRefreshNowAsync();
                        WasSleeping = false;
                    });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KeepAlive] Failed to setup sleep/wake observers: {ex.Message}");
            }
        }

        private void TeardownMacSleepWakeListeners()
        {
            try
            {
                if (_sleepObserver != null)
                {
                    NSNotificationCenter.DefaultCenter.RemoveObserver(_sleepObserver);
                    _sleepObserver.Dispose();
                    _sleepObserver = null!;
                }
                if (_wakeObserver != null)
                {
                    NSNotificationCenter.DefaultCenter.RemoveObserver(_wakeObserver);
                    _wakeObserver.Dispose();
                    _wakeObserver = null!;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KeepAlive] Failed to teardown sleep/wake observers: {ex.Message}");
            }
        }
#endif
    }
}
