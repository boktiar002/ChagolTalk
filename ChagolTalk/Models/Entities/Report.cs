using System.ComponentModel.DataAnnotations;
using ChagolTalk.Models.Enums;
using ChagolTalk.Models.Identity;

namespace ChagolTalk.Models.Entities
{
    /// <summary>
    /// One user flagging another for behaviour during a conversation.
    /// </summary>
    public class Report
    {
        public Guid Id { get; set; }

        [Required]
        public string ReporterId { get; set; } = null!;

        [Required]
        public string ReportedUserId { get; set; } = null!;

        public Guid? ConversationId { get; set; }

        public ReportReason Reason { get; set; } = ReportReason.Other;

        [MaxLength(500)]
        public string? Details { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool Reviewed { get; set; }

        public ApplicationUser Reporter { get; set; } = null!;

        public ApplicationUser ReportedUser { get; set; } = null!;
    }
}
