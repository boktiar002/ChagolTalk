namespace ChagolTalk.Interfaces
{
    /// <summary>
    /// Tracks which users currently have a live SignalR connection.
    /// A user can have more than one connection (multiple tabs/devices),
    /// so we only consider them offline once their last connection drops.
    /// </summary>
    public interface IPresenceTracker
    {
        /// <summary>Returns true if this was the user's first active connection.</summary>
        bool Connect(string userId, string connectionId);

        /// <summary>Returns true if this was the user's last active connection.</summary>
        bool Disconnect(string userId, string connectionId);

        /// <summary>
        /// True while the user holds at least one live connection to this
        /// process. Deliberately not backed by ApplicationUser.IsOnline: that
        /// flag is only updated when a disconnect is actually observed, so it
        /// stays stale forever if the process dies mid-conversation. In-memory
        /// state is empty after a restart, which is the correct answer -- a
        /// restart really does drop every connection.
        /// </summary>
        bool IsOnline(string userId);

        int OnlineCount { get; }
    }
}
