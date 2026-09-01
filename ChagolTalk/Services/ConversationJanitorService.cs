using ChagolTalk.Data;
using ChagolTalk.Hubs;
using ChagolTalk.Interfaces;
using ChagolTalk.Models.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ChagolTalk.Services
{
    /// <summary>
    /// Closes out conversations nobody is in any more.
    ///
    /// A conversation is only marked Ended when someone explicitly leaves
    /// (the hub's End/Skip/Logout path) or when the tab-close beacon fires.
    /// Neither happens on a browser crash, a closed laptop or a dropped
    /// mobile connection, so those rows sat Active forever -- inflating the
    /// "people talking" counter on the lobby, which counts active
    /// conversations and never went back down.
    /// </summary>
    public class ConversationJanitorService : BackgroundService
    {
        /// <summary>
        /// How long both participants must be continuously absent before we
        /// call it. Measured across sweeps rather than from StartedAt: this
        /// process loses all presence state on restart, and Render's free tier
        /// cold-starts slowly enough that clients can take longer than one
        /// sweep to reconnect. Judging on a single observation would reap
        /// conversations that were about to come back.
        /// </summary>
        private static readonly TimeSpan AbandonedAfter = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Backstop for the tab left open forever: with one participant still
        /// nominally connected the presence check alone would never fire.
        /// </summary>
        private static readonly TimeSpan MaxConversationAge = TimeSpan.FromHours(12);

        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Conversation id -> the first sweep at which it looked abandoned.
        /// Rebuilt every sweep, so a conversation that comes back to life
        /// drops out and starts from scratch if it goes quiet again, and
        /// entries for conversations that are no longer Active are discarded.
        /// Only ever touched from the single sweep loop, so it needs no lock.
        /// </summary>
        private Dictionary<Guid, DateTime> _abandonedSince = new();

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IPresenceTracker _presence;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly ILogger<ConversationJanitorService> _logger;

        public ConversationJanitorService(
            IServiceScopeFactory scopeFactory,
            IPresenceTracker presence,
            IHubContext<ChatHub> hubContext,
            ILogger<ConversationJanitorService> logger)
        {
            _scopeFactory = scopeFactory;
            _presence = presence;
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);

            while (!stoppingToken.IsCancellationRequested &&
                   await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await SweepAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // An unhandled throw here would stop the BackgroundService
                    // for the rest of the process lifetime, so a transient
                    // database blip must not be allowed to escape the loop.
                    _logger.LogError(ex, "Conversation sweep failed; will retry next tick.");
                }
            }
        }

        private async Task SweepAsync(CancellationToken cancellationToken)
        {
            // BackgroundService is a singleton and ApplicationDbContext is
            // scoped, so the context has to come from a scope we own.
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var now = DateTime.UtcNow;
            var ageCutoff = now - MaxConversationAge;

            var active = await db.Conversations
                .Where(c => c.Status == ConversationStatus.Active)
                .ToListAsync(cancellationToken);

            var stillAbandoned = new Dictionary<Guid, DateTime>();
            var ended = new List<Guid>();

            foreach (var conversation in active)
            {
                var expired = conversation.StartedAt < ageCutoff;

                var abandoned =
                    !_presence.IsOnline(conversation.User1Id) &&
                    !_presence.IsOnline(conversation.User2Id);

                if (!expired && !abandoned)
                    continue;

                if (!expired)
                {
                    // Carry forward the sweep we first noticed them missing,
                    // so the clock measures the outage rather than this tick.
                    var since = _abandonedSince.TryGetValue(conversation.Id, out var seen)
                        ? seen
                        : now;

                    if (now - since < AbandonedAfter)
                    {
                        stillAbandoned[conversation.Id] = since;
                        continue;
                    }
                }

                conversation.Status = ConversationStatus.Ended;
                conversation.EndedAt = now;

                // EndedByUserId stays null on purpose -- the entity documents
                // null as "the conversation timed out", which is exactly this.

                await BumpConversationStats(db, conversation.User1Id, cancellationToken);
                await BumpConversationStats(db, conversation.User2Id, cancellationToken);

                ended.Add(conversation.Id);
            }

            _abandonedSince = stillAbandoned;

            if (ended.Count == 0)
                return;

            await db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Closed {Count} abandoned conversation(s).", ended.Count);

            // Usually nobody is listening -- that's why the room was reaped --
            // but a half-present client still gets told rather than sitting on
            // a conversation the server has already closed.
            foreach (var id in ended)
            {
                await _hubContext.Clients.Group(id.ToString())
                    .SendAsync("ConversationEnded", cancellationToken: cancellationToken);
            }
        }

        /// <summary>
        /// Mirrors the hub's normal end-of-conversation bookkeeping. Reaped
        /// conversations really did happen, and skipping this would make the
        /// counter quietly under-report exactly the ones that ended badly.
        /// </summary>
        private static async Task BumpConversationStats(
            ApplicationDbContext db,
            string userId,
            CancellationToken cancellationToken)
        {
            var user = await db.Users
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            if (user != null)
                user.TotalConversations++;
        }
    }
}
