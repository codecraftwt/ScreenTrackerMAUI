using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ScreenTracker1.Models;
using Microsoft.JSInterop; // Required for [JSInvokable]

namespace ScreenTracker1.Services
{
    // NOTE: This class is defined as 'partial' to allow the static JSInvokable methods
    // and the instance-based API methods to coexist cleanly.
    public partial class KeyboardMouseService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _serializerOptions;

        // ===============================================
        // 🚨 KEYBOARD/MOUSE TRACKING STATE (The Fix!) 🚨
        // ===============================================

        // A static set to track keys currently held down. This prevents counting repeat events.
        private static readonly HashSet<string> PressedKeys = new HashSet<string>();

        // In KeyboardMouseService.cs

        private static readonly object _lock = new object();
        public static int CurrentKeyboardClicks { get; private set; } = 0;
        public static int CurrentMouseClicks { get; private set; } = 0;

        // CRITICAL: Dictionary to track the last time a key was ACTUALLY counted.
        private static readonly Dictionary<string, DateTime> KeyLastCountTime = new Dictionary<string, DateTime>();
        private const int KeyDebounceMilliseconds = 200; // Ignore repeats for 200ms after the first count.

      
        [JSInvokable]
        public static void HandleKeyDown(string key)
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;

                // 1. Check if the key has been counted recently.
                if (KeyLastCountTime.TryGetValue(key, out var lastCountTime))
                {
                   
                    if ((now - lastCountTime).TotalMilliseconds < KeyDebounceMilliseconds)
                    {
                      
                        return;
                    }
             
                }

                CurrentKeyboardClicks++;

               
                KeyLastCountTime[key] = now;

            }
        }

     

        [JSInvokable]
        public static void IncrementMouseClicks()
        {
            CurrentMouseClicks++;
            Console.WriteLine($"[MOUSE CLICK] Click logged. Current total: {CurrentMouseClicks}");
        }

        // ... (Ensure GetAndResetClicks() is correct, as fixed previously) ...
        public static (int keyboard, int mouse) GetAndResetClicks()
       {
            var keyboardClicksToLog = CurrentKeyboardClicks;
            var mouseClicksToLog = CurrentMouseClicks;

            CurrentKeyboardClicks = 0;
            CurrentMouseClicks = 0;

            return (keyboardClicksToLog, mouseClicksToLog);
        }


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
                // Ensure your token is added if needed for authentication
                // _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "YOUR_TOKEN_HERE");

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