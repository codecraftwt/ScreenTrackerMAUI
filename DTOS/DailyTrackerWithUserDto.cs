using ScreenTracker1.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenTracker1.DTOS
{
    public class DailyTrackerWithUserDto
    {
        public DailyTracker Tracker { get; set; }
        public User User { get; set; }

    }
}
