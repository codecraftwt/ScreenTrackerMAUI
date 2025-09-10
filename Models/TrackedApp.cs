using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenTracker1.Models
{
    public class TrackedApp
    {
        public string AppName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }

        public TimeSpan TotalTime =>
        (EndTime ?? DateTime.Now) - StartTime;

    }
}
