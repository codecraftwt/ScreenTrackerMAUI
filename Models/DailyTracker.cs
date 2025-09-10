using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenTracker1.Models
{
    public class DailyTracker
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime Date { get; set; } = (DateTime.UtcNow.Date);
        public DateTime? StartTracker { get; set; }
        public DateTime? EndTracker { get; set; }
        public TimeSpan? AfkTime { get; set; } = TimeSpan.Zero;
        public TimeSpan? ActiveTime { get; set; } = TimeSpan.Zero;
        public TimeSpan? TotalTime { get; set; } = TimeSpan.Zero;
        public bool IsActive { get; set; }
        public string? UserName { get; set; }

    }
}
