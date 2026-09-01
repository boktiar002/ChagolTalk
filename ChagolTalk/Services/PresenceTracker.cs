using System.Collections.Concurrent;
using ChagolTalk.Interfaces;

namespace ChagolTalk.Services
{
    public class PresenceTracker : IPresenceTracker
    {
        private readonly ConcurrentDictionary<string, HashSet<string>> _connections = new();
        private readonly object _lock = new();

        public int OnlineCount
        {
            get { lock (_lock) return _connections.Count; }
        }

        public bool Connect(string userId, string connectionId)
        {
            lock (_lock)
            {
                if (!_connections.TryGetValue(userId, out var set))
                {
                    set = new HashSet<string>();
                    _connections[userId] = set;
                }

                var wasEmpty = set.Count == 0;
                set.Add(connectionId);
                return wasEmpty;
            }
        }

        public bool Disconnect(string userId, string connectionId)
        {
            lock (_lock)
            {
                if (!_connections.TryGetValue(userId, out var set))
                    return false;

                set.Remove(connectionId);

                if (set.Count == 0)
                {
                    _connections.TryRemove(userId, out _);
                    return true;
                }

                return false;
            }
        }

        public bool IsOnline(string userId)
        {
            // Entries are removed as soon as their last connection drops, so
            // presence is just "do we still have a bucket for them".
            lock (_lock)
            {
                return _connections.TryGetValue(userId, out var set) && set.Count > 0;
            }
        }
    }
}
