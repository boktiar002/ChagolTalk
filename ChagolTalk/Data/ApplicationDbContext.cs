using ChagolTalk.Models.Entities;
using ChagolTalk.Models.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ChagolTalk.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Conversation> Conversations { get; set; } = null!;
        public DbSet<Message> Messages { get; set; } = null!;
        public DbSet<Report> Reports { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Conversation>()
                .HasOne(c => c.User1)
                .WithMany()
                .HasForeignKey(c => c.User1Id)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Conversation>()
                .HasOne(c => c.User2)
                .WithMany()
                .HasForeignKey(c => c.User2Id)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Conversation>()
                .HasIndex(c => c.Status);

            builder.Entity<Message>()
                .HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Message>()
                .HasIndex(m => new { m.ConversationId, m.SentAt });

            builder.Entity<Report>()
                .HasOne(r => r.Reporter)
                .WithMany()
                .HasForeignKey(r => r.ReporterId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Report>()
                .HasOne(r => r.ReportedUser)
                .WithMany()
                .HasForeignKey(r => r.ReportedUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Report>()
                .HasIndex(r => r.ReportedUserId);

            // Declared explicitly even though it matches the one EF creates by
            // convention for the FK. The filtered composite index below leads
            // with ReporterId, so EF considers this redundant and drops it --
            // but a filtered index can't serve lookups on rows the filter
            // excludes, and dropping an index on a live table is not something
            // this migration should be doing as a side effect.
            builder.Entity<Report>()
                .HasIndex(r => r.ReporterId);

            // Database-level backing for the duplicate check in ChatHub.
            // SignalR runs one invocation at a time per connection, so the
            // in-hub check already covers the realistic abuse case, but a user
            // with two tabs open holds two connections and could still race
            // past it. This makes the second insert fail rather than count.
            // Filtered because ConversationId is nullable and reports that
            // aren't tied to a conversation shouldn't collide with each other.
            builder.Entity<Report>()
                .HasIndex(r => new { r.ReporterId, r.ConversationId })
                .IsUnique()
                .HasFilter("\"ConversationId\" IS NOT NULL");
        }
    }
}
