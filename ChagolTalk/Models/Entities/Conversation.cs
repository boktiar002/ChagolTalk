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

        /// <summary>The mode both users agreed on when they were matched.</summary>
        public ChatMode Mode { get; set; } = ChatMode.Any;

        /// <summary>Interest tags both users had in common, comma separated.</summary>
        public string? SharedInterests { get; set; }

        public bool HadVoiceCall { get; set; }

        public int VoiceSeconds { get; set; }

        /// <summary>Who hung up first. Null when the conversation timed out.</summary>
        public string? EndedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ApplicationUser User1 { get; set; } = null!;

        public ApplicationUser User2 { get; set; } = null!;

        public ICollection<Message> Messages { get; set; } = new List<Message>();

        /// <summary>Returns the id of whoever is not <paramref name="userId"/>.</summary>
        public string OtherUserId(string userId) =>
            User1Id == userId ? User2Id : User1Id;

        public bool HasParticipant(string userId) =>
            User1Id == userId || User2Id == userId;
    }
}
