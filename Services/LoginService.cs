using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ScreenTracker1.Models;

namespace ScreenTracker1.Services
{
    public class LoginService
    {
        private readonly HttpClient _httpClient;

        public LoginService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        public async Task<string> LoginAsync(LoginModel loginModel)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{App.URL}Auth/login", loginModel);
                var body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // Try to extract token from JSON response
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        if (doc.RootElement.TryGetProperty("token", out var t))
                            return t.GetString()?.Trim() ?? body;
                        if (doc.RootElement.TryGetProperty("accessToken", out var at))
                            return at.GetString()?.Trim() ?? body;
                        if (doc.RootElement.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object)
                        {
                            if (d.TryGetProperty("token", out var dt))
                                return dt.GetString()?.Trim() ?? body;
                        }
                        if (doc.RootElement.ValueKind == JsonValueKind.String)
                            return doc.RootElement.GetString()?.Trim() ?? body;
                    }
                    catch (JsonException)
                    {
                        // Not JSON, return raw body (backward compatibility)
                    }
                    return body.Trim();
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    // Try to extract error message from JSON response
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        if (doc.RootElement.TryGetProperty("message", out var msg))
                            return msg.GetString() ?? "Invalid credentials";
                        if (doc.RootElement.TryGetProperty("title", out var title))
                            return title.GetString() ?? "Invalid credentials";
                    }
                    catch (JsonException)
                    {
                        // Not JSON, return raw body
                    }
                    return string.IsNullOrEmpty(body) ? "Invalid credentials" : body;
                }
                else
                {
                    return "Login failed. Please try again.";
                }
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
        public async Task<string> SendOtpAsync(OtpRequestModel otpRequest)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{App.URL}Auth/sendOTP", otpRequest);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    return $"OTP sent successfully to {otpRequest.Email}. Response: {result}";
                }
                else if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    return "Failed to send OTP. Invalid email address.";
                }

                return "Failed to send OTP. Please try again.";
            }
            catch (Exception ex)
            {
                return $"An error occurred while sending OTP: {ex.Message}";
            }
        }

        public async Task<string> VerifyOtpAsync(VerifyOtpRequestModel verifyOtpRequest)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{App.URL}Auth/verifyOTP", verifyOtpRequest);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    return $"OTP verification successful. Response: {result}";
                }
                else if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    return "Invalid OTP, email, or password format.";
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return "OTP expired or unauthorized.";
                }

                return "OTP verification failed. Please try again.";
            }
            catch (Exception ex)
            {
                return $"An error occurred while verifying OTP: {ex.Message}";
            }
        }


    }
}
