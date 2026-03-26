using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenTracker1.Models
{
    public class AppUsageData
    {
        public string date { get; set; }
        public double totalDurationInMinutes { get; set; }

        public string afkTime { get; set; } 
        public string activeTime { get; set; }  

        public double AfkDurationInMinutes => ConvertTimeStringToMinutes(afkTime);
        public double ActiveDurationInMinutes => ConvertTimeStringToMinutes(activeTime);

        private static double ConvertTimeStringToMinutes(string timeString)
        {
            if (string.IsNullOrEmpty(timeString) || timeString == "00:00:00")
            {
                return 0.0;
            }

            if (TimeSpan.TryParse(timeString, out TimeSpan timeSpan))
            {
                return timeSpan.TotalMinutes;
            }

            Console.WriteLine($"Warning: Could not parse time string: {timeString}");
            return 0.0;
        }
    }
}
