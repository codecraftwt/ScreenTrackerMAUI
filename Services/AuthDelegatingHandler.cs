using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ScreenTracker1.Services
{
    public class AuthDelegatingHandler : DelegatingHandler
    {
        private readonly DeviceIdProvider _deviceIdProvider;

        private static readonly HashSet<string> _noAuthEndpoints = new(StringComparer.OrdinalIgnoreCase)
        {
            "/api/auth/login",
            "/api/auth/register",
            "/api/auth/sendotp",
            "/api/auth/verifyotp",
            "/api/auth/forgot-password",
            "/api/auth/reset-password",
            "/api/auth/google-login",
            "/api/auth/refresh-token",
        };

        public AuthDelegatingHandler(DeviceIdProvider deviceIdProvider)
        {
            _deviceIdProvider = deviceIdProvider;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.AbsolutePath ?? string.Empty;

            bool isAuthEndpoint = _noAuthEndpoints.Any(e =>
                url.EndsWith(e, StringComparison.OrdinalIgnoreCase));

            if (!isAuthEndpoint)
            {
                var token = GetAuthToken();
                if (!string.IsNullOrWhiteSpace(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
            }

            var deviceId = _deviceIdProvider.GetDeviceId();
            request.Headers.Remove("X-Device-Id");
            request.Headers.Add("X-Device-Id", deviceId);

            var response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized && !isAuthEndpoint)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"[AuthHandler] 401 from {url}: {body}");

                var currentToken = GetAuthToken();
                if (!string.IsNullOrEmpty(currentToken) && !Preferences.Get("isLoggingOut", false))
                {
                    bool isExpired = IsTokenExpired(currentToken);
                    Console.WriteLine($"[AuthHandler] Current token expired? {isExpired}");

                    bool refreshed = await TryRefreshTokenAsync();
                    Console.WriteLine($"[AuthHandler] Token refresh result: {refreshed}");

                    if (refreshed)
                    {
                        var newToken = GetAuthToken();
                        var retryRequest = await CloneRequest(request);
                        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
                        response = await base.SendAsync(retryRequest, cancellationToken);
                        Console.WriteLine($"[AuthHandler] Retry result: {response.StatusCode}");
                    }
                    else
                    {
                        if (isExpired)
                        {
                            Console.WriteLine("[AuthHandler] Token expired and refresh failed. Returning 401 to caller.");
                        }
                        else
                        {
                            Console.WriteLine("[AuthHandler] Token is NOT expired but got 401 and refresh failed. DeviceId conflict or session invalidated.");
                            Console.WriteLine($"[AuthHandler] DeviceId: {deviceId}. Returning 401 to caller.");
                        }
                    }
                }
            }

            return response;
        }

        private static string? GetAuthToken()
        {
            var token = Preferences.Get("authToken", string.Empty);
            if (string.IsNullOrEmpty(token))
            {
#if !__MACCATALYST__
                try
                {
                    token = SecureStorage.GetAsync("authToken").Result;
                }
                catch { }
#endif
            }
            if (!string.IsNullOrEmpty(token))
            {
                Console.WriteLine($"[AuthHandler] Token: {token[..Math.Min(token.Length, 30)]}...");
            }
            else
            {
                Console.WriteLine("[AuthHandler] No token found.");
            }
            return string.IsNullOrEmpty(token) ? null : token;
        }

        private static bool IsTokenExpired(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadToken(token) as JwtSecurityToken;
                if (jwtToken == null) return true;
                var exp = jwtToken.ValidTo;
                Console.WriteLine($"[AuthHandler] Token expires: {exp.ToLocalTime()}. Current: {DateTime.Now}. Expired: {exp < DateTime.UtcNow}");
                return exp < DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthHandler] Token parse error: {ex.Message}");
                return true;
            }
        }

        private async Task<bool> TryRefreshTokenAsync()
        {
            if (Preferences.Get("isLoggingOut", false))
            {
                Console.WriteLine("[AuthHandler] Refresh skipped during intentional logout.");
                return false;
            }

            var currentToken = GetAuthToken();
            if (string.IsNullOrEmpty(currentToken))
            {
                Console.WriteLine("[AuthHandler] Refresh: no token found.");
                return false;
            }

            Console.WriteLine("[AuthHandler] Attempting token refresh...");

            try
            {
                using var refreshClient = new HttpClient { BaseAddress = new Uri(App.URL) };

                var payload = JsonSerializer.Serialize(new { token = currentToken });
                var content = new StringContent(payload, Encoding.UTF8, "application/json");

                var response = await refreshClient.PostAsync("auth/refresh-token", content);
                var responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[AuthHandler] Refresh response: HTTP {response.StatusCode} - {responseBody}");

                if (response.IsSuccessStatusCode)
                {
                    if (Preferences.Get("isLoggingOut", false))
                        return false;

                    using var doc = JsonDocument.Parse(responseBody);
                    string? newToken = null;
                    if (doc.RootElement.TryGetProperty("token", out var t))
                        newToken = t.GetString();
                    else if (doc.RootElement.TryGetProperty("accessToken", out var at))
                        newToken = at.GetString();
                    else if (doc.RootElement.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object)
                    {
                        if (d.TryGetProperty("token", out var dt))
                            newToken = dt.GetString();
                    }

                    if (!string.IsNullOrEmpty(newToken))
                    {
                        if (Preferences.Get("isLoggingOut", false))
                            return false;

                        Preferences.Set("authToken", newToken);
                        Console.WriteLine("[AuthHandler] Token refreshed and saved.");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine("[AuthHandler] Refresh response didn't contain 'token' or 'accessToken' field.");
                    }
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    Console.WriteLine("[AuthHandler] Refresh endpoint not found (404). Backend may not support token refresh.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthHandler] Refresh exception: {ex.Message}");
            }

            return false;
        }

        private static async Task<HttpRequestMessage> CloneRequest(HttpRequestMessage request)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri);
            if (request.Content != null)
            {
                var contentBytes = await request.Content.ReadAsByteArrayAsync();
                clone.Content = new ByteArrayContent(contentBytes);
                if (request.Content.Headers.ContentType != null)
                    clone.Content.Headers.ContentType = request.Content.Headers.ContentType;
            }

            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return clone;
        }
    }
}
