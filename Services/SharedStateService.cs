using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenTracker1.Services
{
    public class SharedStateService
    {
        public int? SelectedUserId { get; set; }
        public string SelectedUsername { get; set; }
        public string SelectedUsageType { get; set; } = "all";
    }
}
