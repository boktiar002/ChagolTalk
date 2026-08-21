namespace ChagolTalk.Models.Realtime
{
    public class WaitingUser
    {
        public string UserId { get; set; } = string.Empty;

        public string ConnectionId { get; set; } = string.Empty;

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}