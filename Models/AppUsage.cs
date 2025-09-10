using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenTracker1.Models
{
    public class AppUsage
    {
        public int id { get; set; }
        public string appName { get; set; } = string.Empty;
        public DateTime startTime { get; set; }
        public DateTime endTime { get; set; }
        public double durationInMinutes { get; set; }

        public int userId { get; set; }

    }

    public class AppUsageModel
    {
        public string AppName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
    }

    public class AppTitleModel
    {
        public string AppName { get; set; }
        public string Title { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

     
    }
}
