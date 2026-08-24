using ChagolTalk.Models.Entities;

namespace ChagolTalk.Models.Realtime
{
    /// <summary>
    /// Outcome of a matchmaking attempt.
    /// </summary>
    public class MatchResult
    {
        public static readonly MatchResult Queued = new();

        /// <summary>Null when nobody suitable was waiting and the user was queued instead.</summary>
        public Conversation? Conversation { get; init; }

        public WaitingUser? Partner { get; init; }

        public int SharedInterestCount { get; init; }

        public bool Matched => Conversation is not null;
    }
}
