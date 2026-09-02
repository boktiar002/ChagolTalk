using System.ComponentModel.DataAnnotations;

namespace ChagolTalk.ViewModels.Account
{
    /// <summary>
    /// What a guest has to provide to start talking: a name, and nothing else.
    /// ChagolTalk is open to anyone, so no age is asked at either door. Age is
    /// an optional profile field for people who choose to fill it in.
    /// </summary>
    public class QuickStartViewModel
    {
        [Required]
        [StringLength(20, MinimumLength = 2)]
        public string DisplayName { get; set; } = string.Empty;
    }
}
