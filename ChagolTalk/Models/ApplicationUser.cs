using Microsoft.AspNetCore.Identity;

namespace ChagolTalk.Models
{
    public class ApplicationUser : IdentityUser
    {
        // Profile Information
        public string? DisplayName { get; set; }

        public string? Bio { get; set; }

        public string? Country { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public string? AvatarSeed { get; set; }

        // System Information
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastSeen { get; set; }

        public bool IsOnline { get; set; }

        public bool IsBanned { get; set; }
    }
}