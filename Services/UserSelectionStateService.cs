using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ScreenTracker1.Models;

namespace ScreenTracker1.Services
{
    public class UserSelectionStateService
    {
        public int UserId { get; set; }
        public string? SelectedUsername { get; set; }
        public string? SelectedAdminUsername { get; set; }
        public string? SelectedUsageType { get; set; } = "all";
        public DateTime SelectedDate { get; set; } = DateTime.Today;
        public List<User> Admins { get; set; } = new List<User>();
        public List<User> Users { get; set; } = new List<User>();
    }
}
