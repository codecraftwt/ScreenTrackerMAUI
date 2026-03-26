using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenTracker1.Models
{
    public class Screenshots
    {
        public int id { get; set; }
        public int userId { get; set; }
        public DateTime captureTime { get; set; }
        public string imageUrl { get; set; }
        public string publicId { get; set; }
        public int keyboardClicks { get; set; }
        public int mouseClicks { get; set; }
        public string minuteActivityData { get; set; }

     

    }
}
