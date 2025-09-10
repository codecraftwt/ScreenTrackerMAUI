using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenTracker1.DTOS
{
    public class CategoryTitle
    {
        public int id { get; set; }
        public string appName { get; set; }
        public string title { get; set; }
        public DateTime startTime { get; set; }
        public DateTime endTime { get; set; }
        public double durationInMinutes { get; set; }
        public int userId { get; set; }
        public string category { get; set; }
        public string subcategory { get; set; }
    }
}
