using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ScreenTracker1.Models;

namespace ScreenTracker1.Services
{
    public class KeyboardMouseService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _serializerOptions;

        public KeyboardMouseService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(App.URL),
                DefaultRequestHeaders = { { "Accept", "application/json" } }
            };

            _serializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
        }

        public async Task<Screenshots?> GetImageActivityDataAsync(int imageId)
        {
            try
            {
                using var response = await _httpClient.GetAsync($"Image/{imageId}").ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonSerializer.Deserialize<Screenshots>(content, _serializerOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching image activity data: {ex.Message}");
                return null;
            }
        }

        public Dictionary<string, MinuteActivity>? ParseMinuteActivityData(string? minuteActivityData)
        {
            if (string.IsNullOrEmpty(minuteActivityData))
                return null;

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, MinuteActivity>>(
                    minuteActivityData,
                    _serializerOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing minute activity data: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}