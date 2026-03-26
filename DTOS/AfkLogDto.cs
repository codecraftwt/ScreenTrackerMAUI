using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenTracker1.DTOS
{
    public class AfkLogDto
    {
        public int UserId { get; set; }
        public DateTime AfkStartTime { get; set; }
        public DateTime AfkEndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public string StartMode { get; set; }




    }
}
