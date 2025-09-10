using System.ComponentModel.DataAnnotations;

namespace ScreenTracker1.Models
{
    public class RegisterModel
    {
        [Required]
        public string username { get; set; }

        [Required]
        public string firstName { get; set; }

        [Required]
        public string lastName { get; set; }

        [Required, EmailAddress]
        public string email { get; set; }

        [Required]
        public string phoneNumber { get; set; }

        [Required, MinLength(6)]
        public string Password { get; set; }

        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; }

        [Required]
        public string role { get; set; }
        public int? IsCreatedBy { get; set; }


    }
}