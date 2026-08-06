using System.ComponentModel.DataAnnotations;

namespace ChagolTalk.ViewModels.Account
{
    public class EditProfileViewModel
    {
        [StringLength(30)]
        public string? DisplayName { get; set; }

        [StringLength(200)]
        public string? Bio { get; set; }

        [StringLength(50)]
        public string? Country { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }
    }
}