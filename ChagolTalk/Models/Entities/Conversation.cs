using ChagolTalk.Models.Enums;
using ChagolTalk.Models.Identity;

namespace ChagolTalk.Models.Entities
{
    public class Conversation
    {
        public Guid Id { get; set; }

        public string User1Id { get; set; } = null!;

        public string User2Id { get; set; } = null!;

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        public DateTime? EndedAt { get; set; }

        public ConversationStatus Status { get; set; } = ConversationStatus.Waiting;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ApplicationUser User1 { get; set; } = null!;

        public ApplicationUser User2 { get; set; } = null!;
    }
}