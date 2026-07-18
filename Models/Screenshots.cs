using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ScreenTracker1.Models
{
    public class Screenshots
    {
        public int id { get; set; }
        public int userId { get; set; }
        public DateTime captureTime { get; set; }

        private string? _imageUrl;
        public string imageUrl
        {
            get => _imageUrl ?? image_path ?? string.Empty;
            set => _imageUrl = value;
        }

        public string? image_path { get; set; }

        public string publicId { get; set; }
        public int keyboardClicks { get; set; }
        public int mouseClicks { get; set; }
        public string minuteActivityData { get; set; }

        /// <summary>
        /// Captures extra JSON fields from the API that aren't mapped to properties.
        /// The API may return base64-encoded image data (e.g., "imageData" or "imageBase64").
        /// Use GetBase64ImageUri() to retrieve it as a ready-to-use data URI.
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }

        /// <summary>
        /// Extracts base64 image data from API response extra fields and returns as a data URI.
        /// Checks common field names: imageData, imageBase64, base64Data, fileData.
        /// Returns null if no base64 image data is found.
        /// </summary>
        public string? GetBase64ImageUri()
        {
            if (ExtensionData == null || ExtensionData.Count == 0)
                return null;

            string[] possibleFields = { "imageData", "imageBase64", "base64Data", "fileData", "base64", "data", "file" };

            foreach (var field in possibleFields)
            {
                if (ExtensionData.TryGetValue(field, out var element) && element.ValueKind == JsonValueKind.String)
                {
                    string? raw = element.GetString();
                    if (!string.IsNullOrEmpty(raw) && raw.Length > 100)
                    {
                        // If it's already a data URI, return as-is
                        if (raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                            return raw;

                        // Otherwise, assume it's raw base64 (PNG image)
                        return $"data:image/png;base64,{raw}";
                    }
                }
            }

            return null;
        }
    }
}
