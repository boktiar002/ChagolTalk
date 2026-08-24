using System.ComponentModel.DataAnnotations;

namespace ChagolTalk.ViewModels.Account
{
    /// <summary>The only thing a guest has to provide to start talking.</summary>
    public class QuickStartViewModel
    {
        [Required]
        [StringLength(20, MinimumLength = 2)]
        public string DisplayName { get; set; } = string.Empty;
    }
}
