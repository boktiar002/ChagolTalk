using System.ComponentModel.DataAnnotations;
using ChagolTalk.Configurations;
using ChagolTalk.Helpers;

namespace ChagolTalk.ViewModels.Account
{
    public class RegisterViewModel
    {
        [Required]
        [StringLength(20, MinimumLength = 3)]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your date of birth.")]
        [DataType(DataType.Date)]
        [MinimumAge(AppSettings.MinimumAge)]
        [Display(Name = "Date of birth")]
        public DateTime? DateOfBirth { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [Compare(nameof(Password))]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
