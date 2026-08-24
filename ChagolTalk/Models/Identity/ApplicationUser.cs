using ChagolTalk.Models.Enums;
using Microsoft.AspNetCore.Identity;

namespace ChagolTalk.Models.Identity
{
    public class ApplicationUser : IdentityUser
    {
        // ---------- Profile ----------
        public string? DisplayName { get; set; }

        public string? Bio { get; set; }

        public string? Country { get; set; }

        public DateTime? DateOfBirth { get; set; }

        /// <summary>Deterministic seed used to draw the generated avatar.</summary>
        public string? AvatarSeed { get; set; }

        // ---------- Matching preferences ----------
        /// <summary>Comma separated interest tags, lowercase. Used to bias matchmaking.</summary>
        public string? Interests { get; set; }

        public ChatMode PreferredMode { get; set; } = ChatMode.Voice;

        /// <summary>Spoken language tag (e.g. "en", "bn"). Matched as a soft preference.</summary>
        public string? Language { get; set; }

        // ---------- Stats ----------
        public int TotalConversations { get; set; }

        public int TotalVoiceSeconds { get; set; }

        // ---------- Moderation ----------
        public int ReportCount { get; set; }

        /// <summary>When set in the future the user cannot enter the matching queue.</summary>
        public DateTime? MutedUntil { get; set; }

        public bool IsBanned { get; set; }

        /// <summary>
        /// True for accounts auto-created by the "just start talking" quick
        /// flow (name typed into a popup, no password). Guests can still use
        /// everything -- dashboard, edit profile -- but never had to register.
        /// </summary>
        public bool IsGuest { get; set; }

        // ---------- System ----------
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastSeen { get; set; }

        public bool IsOnline { get; set; }
    }
}
