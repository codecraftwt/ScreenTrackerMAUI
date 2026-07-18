using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ScreenTracker1.DTOS
{
    public  class AppTitle
    {
        public string title { get; set; }
        [JsonPropertyName("DurationMinutes")]
        public double durationInMinutes { get; set; }
        public string appName { get; set; }
        // Used for client-side date filtering when fetching ALL records from AppTitle/user/{id}
        public DateTime startTime { get; set; }
        public DateTime endTime { get; set; }
    }
}
