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

        int OnlineCount { get; }
    }
}
