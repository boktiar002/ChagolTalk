using ChagolTalk.Models.Enums;

namespace ChagolTalk.Models.Realtime
{
    /// <summary>
    /// A user sitting in the matchmaking queue. Lives in memory only.
    /// </summary>
    public class WaitingUser
    {
        public string UserId { get; set; } = string.Empty;

        public string ConnectionId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = "Stranger";

        public ChatMode Mode { get; set; } = ChatMode.Any;

        /// <summary>Lowercase interest tags this user opted in with.</summary>
        public HashSet<string> Interests { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public string? Language { get; set; }

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        /// <summary>How long the user has been queued.</summary>
        public TimeSpan WaitTime => DateTime.UtcNow - JoinedAt;
    }
}
