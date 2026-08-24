using System.Collections.Concurrent;
using ChagolTalk.Interfaces;
using ChagolTalk.Models.Entities;
using ChagolTalk.Models.Enums;
using ChagolTalk.Models.Realtime;

namespace ChagolTalk.Services
{
    /// <summary>
    /// In-memory matchmaking queue.
    ///
    /// Single global lock around the "find or enqueue" decision keeps the
    /// match atomic — two callers can never both dequeue the same waiting
    /// user, which was possible with the old ConcurrentQueue.Any()+TryDequeue
    /// combo (a classic check-then-act race).
    /// </summary>
    public class MatchingService : IMatchingService
    {
        private readonly object _lock = new();

        private readonly List<WaitingUser> _waiting = new();

        // Remembers the last partner for each user for a short cooldown so
        // hitting "Find Another" twice in a row doesn't just bounce two
        // people back to each other.
        private readonly ConcurrentDictionary<string, (string PartnerId, DateTime At)> _lastPartner = new();

        private static readonly TimeSpan RematchCooldown = TimeSpan.FromMinutes(2);

        public int WaitingCount
        {
            get { lock (_lock) return _waiting.Count; }
        }

        public MatchResult FindMatch(WaitingUser user)
        {
            lock (_lock)
            {
                // Already queued — don't duplicate.
                _waiting.RemoveAll(w => w.UserId == user.UserId);

                var candidate = FindBestCandidate(user);

                if (candidate is null)
                {
                    _waiting.Add(user);
                    return MatchResult.Queued;
                }

                _waiting.Remove(candidate);

                var mode = ResolveMode(user.Mode, candidate.Mode);

                var shared = user.Interests
                    .Intersect(candidate.Interests, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var conversation = new Conversation
                {
                    Id = Guid.NewGuid(),
                    User1Id = candidate.UserId,
                    User2Id = user.UserId,
                    StartedAt = DateTime.UtcNow,
                    Status = ConversationStatus.Active,
                    Mode = mode,
                    SharedInterests = shared.Count > 0 ? string.Join(",", shared) : null
                };

                var now = DateTime.UtcNow;
                _lastPartner[user.UserId] = (candidate.UserId, now);
                _lastPartner[candidate.UserId] = (user.UserId, now);

                return new MatchResult
                {
                    Conversation = conversation,
                    Partner = candidate,
                    SharedInterestCount = shared.Count
                };
            }
        }

        /// <summary>
        /// Picks the waiting user that best matches the requester. Must be
        /// called while holding <see cref="_lock"/>.
        /// </summary>
        private WaitingUser? FindBestCandidate(WaitingUser user)
        {
            WaitingUser? best = null;
            var bestScore = int.MinValue;

            foreach (var other in _waiting)
            {
                if (other.UserId == user.UserId)
                    continue;

                if (!ModeCompatible(user.Mode, other.Mode))
                    continue;

                if (IsRecentPartner(user.UserId, other.UserId) && _waiting.Count > 1)
                    continue;

                var score = ScoreCandidate(user, other);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = other;
                }
            }

            // Nobody passed the recent-partner filter — fall back to anyone
            // compatible rather than leaving the user stuck in queue forever.
            if (best is null)
            {
                foreach (var other in _waiting)
                {
                    if (other.UserId == user.UserId)
                        continue;

                    if (!ModeCompatible(user.Mode, other.Mode))
                        continue;

                    var score = ScoreCandidate(user, other);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = other;
                    }
                }
            }

            return best;
        }

        private static int ScoreCandidate(WaitingUser user, WaitingUser other)
        {
            var score = 0;

            var sharedInterests = user.Interests
                .Intersect(other.Interests, StringComparer.OrdinalIgnoreCase)
                .Count();

            score += sharedInterests * 10;

            if (!string.IsNullOrEmpty(user.Language) &&
                string.Equals(user.Language, other.Language, StringComparison.OrdinalIgnoreCase))
            {
                score += 5;
            }

            // Longer-waiting users get priority when scores tie, handled by
            // giving a small time bonus.
            score += (int)Math.Min(other.WaitTime.TotalSeconds / 10, 20);

            return score;
        }

        private bool IsRecentPartner(string userId, string otherUserId)
        {
            if (_lastPartner.TryGetValue(userId, out var last) &&
                last.PartnerId == otherUserId &&
                DateTime.UtcNow - last.At < RematchCooldown)
            {
                return true;
            }

            return false;
        }

        private static bool ModeCompatible(ChatMode a, ChatMode b)
        {
            if (a == ChatMode.Any || b == ChatMode.Any)
                return true;

            return a == b;
        }

        private static ChatMode ResolveMode(ChatMode a, ChatMode b)
        {
            if (a == b)
                return a;

            return a == ChatMode.Any ? b : a;
        }

        public bool LeaveQueue(string userId)
        {
            lock (_lock)
            {
                return _waiting.RemoveAll(w => w.UserId == userId) > 0;
            }
        }

        public bool IsWaiting(string userId)
        {
            lock (_lock)
            {
                return _waiting.Any(w => w.UserId == userId);
            }
        }

        public IReadOnlyList<WaitingUser> PruneStale(TimeSpan maxAge)
        {
            lock (_lock)
            {
                var stale = _waiting.Where(w => w.WaitTime > maxAge).ToList();

                foreach (var user in stale)
                    _waiting.Remove(user);

                return stale;
            }
        }

        public void RememberPairing(string userIdA, string userIdB)
        {
            var now = DateTime.UtcNow;
            _lastPartner[userIdA] = (userIdB, now);
            _lastPartner[userIdB] = (userIdA, now);
        }
    }
}
