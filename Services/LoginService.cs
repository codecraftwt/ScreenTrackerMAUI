using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
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

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    return result; 
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                   
                    return "Invalid credentials. Please check your username and password.";
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
