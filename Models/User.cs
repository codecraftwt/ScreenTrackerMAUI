using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ScreenTracker1.Models
{
    public class User
    {
        [JsonPropertyName("id")]
        public int id { get; set; }
        [JsonPropertyName("username")]
        public string username { get; set; }
        [JsonPropertyName("role")]
        public string role { get; set; }

        [JsonPropertyName("email")]
        public string email { get; set; }
        [JsonPropertyName("password")]
        public string password { get; set; }
        [JsonPropertyName("firstName")]
        public string firstName { get; set; }
        [JsonPropertyName("lastName")]
        public string lastName { get; set; }
        [JsonPropertyName("phoneNumber")]
        public string? phoneNumber { get; set; }
        [JsonPropertyName("createdAt")]
        public DateTime createdAt { get; set; }
        [JsonPropertyName("isSelected")]
        public bool? isSelected { get; set; }
        [JsonPropertyName("deleteAuthBy")]
        public int? deleteAuthBy { get; set; } = 0;
        [JsonPropertyName("isCreatedBy")]
        public int? IsCreatedBy { get; set; }
        [JsonPropertyName("isActive")]
        public bool? isActive { get; set; }
        [JsonPropertyName("isManualTrackingEnabled")]
        public bool? isManualTrackingEnabled { get; set; }
        [JsonPropertyName("manualTrackingAuthBy")]
        public int? manualTrackingAuthBy { get; set; } = 0;
        [JsonPropertyName("isAutoTrackingEnabled")]
        public bool? isAutoTrackingEnabled { get; set; }
        [JsonPropertyName("autoTrackingAuthBy")]
        public int? autoTrackingAuthBy { get; set; } = 0;
        [JsonPropertyName("updatedBy")]
        public int? updatedBy { get; set; }
        [JsonPropertyName("isAdminAuthority")]
        public bool isAdminAuthority { get; set; } = false;
        [JsonPropertyName("adminAuthorityBy")]
        public int? adminAuthorityBy { get; set; } = 0;
        [JsonPropertyName("adminAuthorityAt")]
        public DateTime? adminAuthorityAt { get; set; }
    }
}
