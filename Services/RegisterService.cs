using ScreenTracker1.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ScreenTracker1.Services
{
    public class RegisterService
    {
        private readonly HttpClient _httpClient;

        public RegisterService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        public async Task<string> RegisterAsync(RegisterModel registerModel)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{App.URL}Auth/register", registerModel);

                if (response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                        return "Registration successful";

                    if (response.StatusCode == HttpStatusCode.NoContent)
                        return "You are already registered.";

                    return "Registration successful";
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                var serverMsg = TryExtractServerMessage(errorContent);

                System.Diagnostics.Debug.WriteLine($"[RegisterService] Status: {(int)response.StatusCode}, Body: {errorContent}");

                if (response.StatusCode == HttpStatusCode.Conflict)
                    return serverMsg ?? "Registration failed due to a unique conflict.";

                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    if (serverMsg != null)
                        return serverMsg;
                    var raw = string.IsNullOrEmpty(errorContent) ? "(empty)" : errorContent;
                    if (raw.Length > 300) raw = raw.Substring(0, 300) + "...";
                    return $"Registration failed: server returned 400. Response: {raw}";
                }

                if (serverMsg != null)
                    return serverMsg;

                var fallback = string.IsNullOrEmpty(errorContent) ? "(empty)" : errorContent;
                if (fallback.Length > 300) fallback = fallback.Substring(0, 300) + "...";
                return $"Registration failed. Status: {(int)response.StatusCode}. Response: {fallback}";
            }
            catch (Exception ex)
            {
                return $"An error occurred: {ex.Message}";
            }
        }

        private static string TryExtractServerMessage(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                    return null;

                // ProblemDetails "errors" dictionary (model validation)
                if (root.TryGetProperty("errors", out var errorsProp) && errorsProp.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in errorsProp.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in prop.Value.EnumerateArray())
                            {
                                var errorMsg = item.GetString();
                                if (!string.IsNullOrEmpty(errorMsg))
                                    return errorMsg;
                            }
                        }
                    }
                }

                // "title" field (ProblemDetails)
                if (root.TryGetProperty("title", out var title))
                {
                    var t = title.GetString();
                    if (!string.IsNullOrEmpty(t) && !t.Contains("validation error", StringComparison.OrdinalIgnoreCase))
                        return t;
                }

                // "message" field (custom error responses)
                if (root.TryGetProperty("message", out var msg))
                {
                    var m = msg.GetString();
                    if (!string.IsNullOrEmpty(m))
                        return m;
                }

                // "detail" field (ProblemDetails)
                if (root.TryGetProperty("detail", out var detail))
                {
                    var d = detail.GetString();
                    if (!string.IsNullOrEmpty(d))
                        return d;
                }
            }
            catch { }
            return null;
        }
    }
}
