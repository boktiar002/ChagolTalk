using System.ComponentModel.DataAnnotations;
using ChagolTalk.Configurations;
using ChagolTalk.Helpers;

namespace ChagolTalk.ViewModels.Account
{
    /// <summary>What a guest has to provide to start talking.</summary>
    public class QuickStartViewModel
    {
        [Required]
        [StringLength(20, MinimumLength = 2)]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Asked here too, not just on registration -- the quick start is the
        /// main way people get into a call, and gating only the account form
        /// would leave the busiest door unlocked.
        ///
        /// A year rather than a full date: this modal is the whole onboarding
        /// flow, and four digits keeps it to one line of typing. Registration,
        /// where someone has already chosen to invest a minute, still asks for
        /// the full date.
        /// </summary>
        [Required(ErrorMessage = "Please enter the year you were born.")]
        [MinimumAge(AppSettings.MinimumAge)]
        [Display(Name = "Year of birth")]
        public int? BirthYear { get; set; }
    }
}
