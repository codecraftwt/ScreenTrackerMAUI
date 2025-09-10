using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenTracker1.Models
{
    public class AfkLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public DateTime AfkStartTime { get; set; }

        [Required]
        public DateTime AfkEndTime { get; set; }

        [Required]
        public TimeSpan Duration { get; set; }
    }
}
