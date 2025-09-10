using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenTracker1.Models
{
    public class MinuteActivity
    {
        public int Keyboard { get; set; }
        public int Mouse { get; set; }
        public DateTime? Timestamp { get; set; }
    }
}
