using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenTracker1.Models
{
    public class VerifyOtpRequestModel
    {
        public string email { get; set; }
        public string otp { get; set; }
        public string newPassword { get; set; }
    }
}
