using System.ComponentModel.DataAnnotations;
using ChagolTalk.Configurations;
using ChagolTalk.Helpers;
using ChagolTalk.Models.Enums;

namespace ChagolTalk.ViewModels.Account
{
    public class EditProfileViewModel
    {
        [StringLength(30, MinimumLength = 2)]
        [Display(Name = "Display name")]
        public string? DisplayName { get; set; }

        [StringLength(200)]
        public string? Bio { get; set; }

        [StringLength(50)]
        public string? Country { get; set; }

        [DataType(DataType.Date)]
        [MinimumAge(AppSettings.MinimumAge)]
        [Display(Name = "Date of birth")]
        public DateTime? DateOfBirth { get; set; }

        [StringLength(150)]
        [Display(Name = "Interests (comma separated)")]
        public string? Interests { get; set; }

        [StringLength(30)]
        public string? Language { get; set; }

        [Display(Name = "Preferred chat mode")]
        public ChatMode PreferredMode { get; set; } = ChatMode.Voice;

        public string? AvatarSeed { get; set; }
    }
}
