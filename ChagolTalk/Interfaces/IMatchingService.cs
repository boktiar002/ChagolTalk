using ChagolTalk.Models.Realtime;

namespace ChagolTalk.Interfaces
{
    public interface IMatchingService
    {
        /// <summary>
        /// Tries to pair <paramref name="user"/> with somebody already waiting.
        /// When nobody suitable is available the user is added to the queue and
        /// <see cref="MatchResult.Matched"/> is false.
        /// </summary>
        MatchResult FindMatch(WaitingUser user);

        /// <summary>Removes the user from the queue. Returns true if they were queued.</summary>
        bool LeaveQueue(string userId);

        bool IsWaiting(string userId);

        int WaitingCount { get; }

        /// <summary>
        /// Drops entries that have been waiting longer than <paramref name="maxAge"/>
        /// and returns them so the caller can notify those connections.
        /// </summary>
        IReadOnlyList<WaitingUser> PruneStale(TimeSpan maxAge);

        /// <summary>
        /// Remembers that two users just spoke so the matcher avoids pairing them
        /// again immediately when they both hit "next".
        /// </summary>
        void RememberPairing(string userIdA, string userIdB);

        /// <summary>
        /// Rolling estimate of how long a new arrival waits before being
        /// matched, based on recent real match outcomes. Falls back to a
        /// sane default when there isn't enough history yet.
        /// </summary>
        TimeSpan EstimatedWaitTime { get; }
    }
}
