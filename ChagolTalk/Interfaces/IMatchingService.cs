using ChagolTalk.Models.Entities;

namespace ChagolTalk.Interfaces
{
    public interface IMatchingService
    {
        Task<(Conversation? Conversation, string? MatchedConnectionId)>
            FindMatchAsync(string userId, string connectionId);

        Task LeaveQueueAsync(string userId);

        bool IsWaiting(string userId);
    }
}