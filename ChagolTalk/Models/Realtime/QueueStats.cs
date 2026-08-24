namespace ChagolTalk.Models.Realtime
{
    /// <summary>Live counters shown on the lobby and landing page.</summary>
    public record QueueStats(int Online, int Waiting, int ActiveConversations);
}
