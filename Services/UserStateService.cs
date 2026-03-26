using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ScreenTracker1.Models;

namespace ScreenTracker1.Services
{
    public class UserStateService
    {
        // Existing properties
        public string? SelectedUsername { get; set; }
        public string? SelectedAdminUsername { get; set; }
        public int UserId { get; set; }
        public List<User>? Users { get; set; }
        public string SelectedUsageType { get; set; } = "all";

        // NEW: Event and method for notification
        public event Action? OnLogoutResetRequired;

        public void TriggerLogoutReset()
        {
            // 1. Clear the state properties
            SelectedUsername = null;
            SelectedAdminUsername = null;
            UserId = 0;
            SelectedUsageType = "all";
            Users = null;

            // 2. Notify all subscribers (User.razor)
            OnLogoutResetRequired?.Invoke();
        }
    }
}
