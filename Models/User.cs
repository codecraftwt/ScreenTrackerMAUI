using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenTracker1.Models
{
    public class User
    {
        public int id { get; set; }
        public string username { get; set; }
        public string role { get; set; }

        public string email { get; set; }
        [Required]
        public string password { get; set; }
        public string firstName { get; set; }
        public string lastName { get; set; }
        public string phoneNumber { get; set; }
        public DateTime createdAt { get; set; }
        public bool? isSelected { get; set; }
        public int? deleteAuthBy { get; set; } = 0;
        public int? IsCreatedBy { get; set; }
    }
}