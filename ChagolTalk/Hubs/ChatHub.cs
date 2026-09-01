using ChagolTalk.Data;
using ChagolTalk.Interfaces;
using ChagolTalk.Models.Entities;
using ChagolTalk.Models.Enums;
using ChagolTalk.Models.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ChagolTalk.Hubs
{
    /// <summary>
    /// Real-time hub for matchmaking, text chat and WebRTC voice signalling.
    ///
    /// IMPORTANT: a new Hub instance is created per method invocation, so
    /// nothing here can be stored as an instance field across calls. Anything
    /// that must survive between calls for the same connection lives in
    /// Context.Items; anything that must be shared across users lives in a
    /// singleton service (IMatchingService, IPresenceTracker).
    /// </summary>
    [Authorize]
    public class ChatHub : Hub
    {
        private const int MaxMessageLength = 2000;
        private static readonly TimeSpan MessageCooldown = TimeSpan.FromMilliseconds(400);

        private readonly IMatchingService _matchingService;
        private readonly IPresenceTracker _presence;
        private readonly ApplicationDbContext _context;

        public ChatHub(
            IMatchingService matchingService,
            IPresenceTracker presence,
            ApplicationDbContext context)
        {
            _matchingService = matchingService;
            _presence = presence;
            _context = context;
        }

        private string? UserId => Context.UserIdentifier;

        // ==========================================
        // CONNECTION LIFECYCLE
        // ==========================================

        public override async Task OnConnectedAsync()
        {
            var userId = UserId;

            if (!string.IsNullOrEmpty(userId))
            {
                var firstConnection = _presence.Connect(userId, Context.ConnectionId);

                if (firstConnection)
                {
                    var user = await _context.Users.FindAsync(userId);

                    if (user != null)
                    {
                        user.IsOnline = true;
                        user.LastSeen = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }

                    await Clients.All.SendAsync("OnlineCount", _presence.OnlineCount);
                }
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = UserId;

            if (!string.IsNullOrEmpty(userId))
            {
                _matchingService.LeaveQueue(userId);

                var lastConnection = _presence.Disconnect(userId, Context.ConnectionId);

                if (lastConnection)
                {
                    var user = await _context.Users.FindAsync(userId);

                    if (user != null)
                    {
                        user.IsOnline = false;
                        user.LastSeen = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }

                    await Clients.All.SendAsync("OnlineCount", _presence.OnlineCount);
                }

                // If they were mid-conversation, let the other side know so
                // they are not left staring at a dead screen.
                var activeConversation = await _context.Conversations
                    .Where(c => c.Status == ConversationStatus.Active)
                    .Where(c => c.User1Id == userId || c.User2Id == userId)
                    .FirstOrDefaultAsync();

                if (activeConversation != null)
                {
                    await Clients.OthersInGroup(activeConversation.Id.ToString())
                        .SendAsync("StrangerDisconnected");
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        // ==========================================
        // MATCHMAKING
        // ==========================================

        /// <summary>
        /// mode: "voice" | "text" | "any". interests: comma separated tags.
        /// </summary>
        public async Task StartMatching(string? mode, string? interests, string? language)
        {
            var userId = UserId;

            if (string.IsNullOrEmpty(userId))
                return;

            var user = await _context.Users.FindAsync(userId);

            if (user == null)
                return;

            if (user.IsBanned)
            {
                await Clients.Caller.SendAsync("MatchingBlocked", "Your account has been suspended.");
                return;
            }

            if (user.MutedUntil.HasValue && user.MutedUntil > DateTime.UtcNow)
            {
                var minutes = Math.Ceiling((user.MutedUntil.Value - DateTime.UtcNow).TotalMinutes);
                await Clients.Caller.SendAsync("MatchingBlocked", $"You can start a new chat in {minutes} minute(s).");
                return;
            }

            // The lobby is a single button with no interest or language
            // pickers, so it sends neither -- whatever the user saved on their
            // profile is the only matching signal the server will ever get.
            // Falling back to it is what makes the scoring in MatchingService
            // do anything at all.
            //
            // Mode is deliberately NOT taken from user.PreferredMode. Interests
            // and language only add to a candidate's score, so a stale value
            // there can never stop someone being matched. Mode *filters* who is
            // eligible, and PreferredMode defaults to Voice for everyone
            // (guests included), so honouring it here would split the pool back
            // into the segments the one-button lobby exists to avoid.
            var effectiveInterests = string.IsNullOrWhiteSpace(interests)
                ? user.Interests
                : interests;

            var effectiveLanguage = string.IsNullOrWhiteSpace(language)
                ? user.Language
                : language;

            var waitingUser = new WaitingUser
            {
                UserId = userId,
                ConnectionId = Context.ConnectionId,
                DisplayName = user.DisplayName ?? "Stranger",
                Mode = ParseMode(mode),
                Language = effectiveLanguage,
                Interests = (effectiveInterests ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Take(10)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)
            };

            var result = _matchingService.FindMatch(waitingUser);

            if (!result.Matched)
            {
                await Clients.Caller.SendAsync(
                    "WaitingForMatch",
                    _matchingService.WaitingCount,
                    (int)_matchingService.EstimatedWaitTime.TotalSeconds);
                return;
            }

            var conversation = result.Conversation!;

            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();

            var partner = result.Partner!;

            await Clients.Caller.SendAsync(
                "MatchFound",
                conversation.Id,
                partner.DisplayName,
                conversation.SharedInterests,
                conversation.Mode.ToString());

            // Route by user, not the connection id captured while they were
            // queued -- that connection may have gone away and been replaced
            // by a reconnect, in which case Clients.Client(...) would silently
            // send nowhere.
            await Clients.User(partner.UserId).SendAsync(
                "MatchFound",
                conversation.Id,
                waitingUser.DisplayName,
                conversation.SharedInterests,
                conversation.Mode.ToString());
        }

        public Task CancelMatching()
        {
            var userId = UserId;

            if (!string.IsNullOrEmpty(userId))
                _matchingService.LeaveQueue(userId);

            return Task.CompletedTask;
        }

        private static ChatMode ParseMode(string? mode) => mode?.ToLowerInvariant() switch
        {
            "voice" => ChatMode.Voice,
            "text" => ChatMode.Text,
            _ => ChatMode.Any
        };

        // ==========================================
        // JOIN CONVERSATION
        // ==========================================

        public async Task JoinConversation(Guid conversationId)
        {
            var userId = UserId;

            if (string.IsNullOrEmpty(userId))
                throw new HubException("User is not authenticated.");

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
                throw new HubException("Conversation not found.");

            if (!conversation.HasParticipant(userId))
                throw new HubException("You are not a participant in this conversation.");

            if (conversation.Status == ConversationStatus.Ended)
                throw new HubException("This conversation has already ended.");

            var groupName = conversationId.ToString();

            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            var partnerId = conversation.OtherUserId(userId);
            var partner = await _context.Users.FindAsync(partnerId);

            await Clients.Caller.SendAsync(
                "JoinedConversation",
                conversationId,
                partner?.DisplayName ?? "Stranger",
                conversation.Mode.ToString(),
                conversation.SharedInterests);

            // Tell the other participant (if already in the room) that this
            // side reconnected, so their UI can clear any "disconnected" banner.
            await Clients.OthersInGroup(groupName).SendAsync("StrangerReconnected");
        }

        // ==========================================
        // TEXT MESSAGING
        // ==========================================

        public async Task SendMessage(Guid conversationId, string message)
        {
            var userId = UserId;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrWhiteSpace(message))
                return;

            message = message.Trim();

            if (message.Length > MaxMessageLength)
                message = message[..MaxMessageLength];

            if (!CheckAndUpdateRateLimit())
                return;

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null || conversation.Status == ConversationStatus.Ended)
                return;

            if (!conversation.HasParticipant(userId))
                throw new HubException("You are not a participant in this conversation.");

            var user = await _context.Users.FindAsync(userId);
            var userName = user?.DisplayName ?? "Stranger";

            _context.Messages.Add(new Message
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                SenderId = userId,
                Content = message,
                Type = MessageType.Text
            });

            await _context.SaveChangesAsync();

            var groupName = conversationId.ToString();
            var sentAt = DateTime.UtcNow;

            await Clients.GroupExcept(groupName, new[] { Context.ConnectionId })
                .SendAsync("ReceiveMessage", userName, message, sentAt);

            await Clients.Caller.SendAsync("ReceiveOwnMessage", userName, message, sentAt);
        }

        /// <summary>Simple per-connection throttle stored in Context.Items (survives across calls for this connection).</summary>
        private bool CheckAndUpdateRateLimit()
        {
            var now = DateTime.UtcNow;

            if (Context.Items.TryGetValue("LastMessageAt", out var lastObj) && lastObj is DateTime last)
            {
                if (now - last < MessageCooldown)
                    return false;
            }

            Context.Items["LastMessageAt"] = now;
            return true;
        }

        public async Task Typing(Guid conversationId, bool isTyping)
        {
            var userId = UserId;

            if (string.IsNullOrEmpty(userId))
                return;

            await Clients.OthersInGroup(conversationId.ToString())
                .SendAsync("StrangerTyping", isTyping);
        }

        // ==========================================
        // ENDING / LEAVING
        // ==========================================

        public async Task EndConversation(Guid conversationId)
        {
            await EndConversationInternal(conversationId);
        }

        public async Task LogoutFromChat(Guid conversationId)
        {
            await EndConversationInternal(conversationId);
        }

        /// <summary>
        /// Ends the current conversation and immediately rejoins the queue
        /// for a new one in a single round trip, instead of making the user
        /// go through the "conversation ended" screen and click again.
        /// </summary>
        public async Task SkipConversation(Guid conversationId, string? mode, string? interests, string? language)
        {
            await EndConversationInternal(conversationId);
            await StartMatching(mode, interests, language);
        }

        private async Task<bool> EndConversationInternal(Guid conversationId)
        {
            var userId = UserId;

            if (string.IsNullOrEmpty(userId))
                return false;

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
                return false;

            if (!conversation.HasParticipant(userId))
                throw new HubException("You are not a participant in this conversation.");

            if (conversation.Status == ConversationStatus.Ended)
                return false;

            conversation.Status = ConversationStatus.Ended;
            conversation.EndedAt = DateTime.UtcNow;
            conversation.EndedByUserId = userId;

            await _context.SaveChangesAsync();

            await BumpConversationStats(conversation.User1Id);
            await BumpConversationStats(conversation.User2Id);

            await _context.SaveChangesAsync();

            _matchingService.RememberPairing(conversation.User1Id, conversation.User2Id);

            var groupName = conversationId.ToString();

            // Only the OTHER participant gets "ConversationEnded" -- the
            // person who clicked End/Skip already knows and updates their
            // own UI locally once the invoke resolves.
            await Clients.OthersInGroup(groupName).SendAsync("ConversationEnded");

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

            return true;
        }

        private async Task BumpConversationStats(string userId)
        {
            var user = await _context.Users.FindAsync(userId);

            if (user != null)
                user.TotalConversations++;
        }

        public async Task LeaveConversation(Guid conversationId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId.ToString());
        }

        public async Task FindAnother(string? mode, string? interests, string? language)
        {
            await StartMatching(mode, interests, language);
        }

        // ==========================================
        // REPORTING
        // ==========================================

        public async Task SubmitReport(Guid conversationId, int reason, string? details)
        {
            var userId = UserId;

            if (string.IsNullOrEmpty(userId))
                return;

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null || !conversation.HasParticipant(userId))
                return;

            // One report per conversation per reporter. Without this the
            // auto-mute below is a weapon rather than a safeguard: SubmitReport
            // has no cooldown of its own, so a single user could call it five
            // times against whoever they were just matched with and silence
            // that person for an hour.
            var alreadyReported = await _context.Reports.AnyAsync(r =>
                r.ReporterId == userId && r.ConversationId == conversationId);

            if (alreadyReported)
            {
                // Acknowledged exactly like a first report -- there is nothing
                // to gain from telling someone their duplicate was dropped.
                await Clients.Caller.SendAsync("ReportSubmitted");
                return;
            }

            var reportedUserId = conversation.OtherUserId(userId);

            var report = new Report
            {
                Id = Guid.NewGuid(),
                ReporterId = userId,
                ReportedUserId = reportedUserId,
                ConversationId = conversationId,
                Reason = Enum.IsDefined(typeof(ReportReason), reason) ? (ReportReason)reason : ReportReason.Other,
                Details = string.IsNullOrWhiteSpace(details) ? null : details[..Math.Min(details.Length, 500)]
            };

            _context.Reports.Add(report);

            var reportedUser = await _context.Users.FindAsync(reportedUserId);

            if (reportedUser != null)
            {
                reportedUser.ReportCount++;

                // Basic automatic moderation: enough reports and the account
                // gets a cooldown from matching. Manual review still applies
                // for bans.
                if (reportedUser.ReportCount >= 5 && reportedUser.ReportCount % 5 == 0)
                {
                    reportedUser.MutedUntil = DateTime.UtcNow.AddHours(1);
                }
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Lost a race against another connection belonging to the same
                // user -- the unique index rejected the duplicate, which is the
                // outcome we wanted anyway. Both the report row and the
                // ReportCount increment roll back together. Answered as success
                // because the client leaves its button stuck on "Sending..."
                // whenever an invoke throws.
            }

            await Clients.Caller.SendAsync("ReportSubmitted");
        }

        // ==========================================
        // WEBRTC VOICE SIGNALLING
        // ==========================================

        public async Task StartVoiceCall(Guid conversationId)
        {
            if (!await IsActiveParticipant(conversationId))
                return;

            await Clients.OthersInGroup(conversationId.ToString()).SendAsync("IncomingVoiceCall");
        }

        public async Task DeclineVoiceCall(Guid conversationId)
        {
            if (string.IsNullOrEmpty(UserId))
                return;

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null || !conversation.HasParticipant(UserId!))
                return;

            await Clients.OthersInGroup(conversationId.ToString()).SendAsync("VoiceCallDeclined");
        }

        public async Task SendVoiceOffer(Guid conversationId, string offer)
        {
            if (!await IsActiveParticipant(conversationId))
                return;

            await Clients.OthersInGroup(conversationId.ToString()).SendAsync("ReceiveVoiceOffer", offer);
        }

        public async Task SendVoiceAnswer(Guid conversationId, string answer)
        {
            if (!await IsActiveParticipant(conversationId))
                return;

            await Clients.OthersInGroup(conversationId.ToString()).SendAsync("ReceiveVoiceAnswer", answer);
        }

        public async Task SendIceCandidate(Guid conversationId, string candidate)
        {
            if (!await IsActiveParticipant(conversationId))
                return;

            await Clients.OthersInGroup(conversationId.ToString()).SendAsync("ReceiveIceCandidate", candidate);
        }

        public async Task EndVoiceCall(Guid conversationId, int durationSeconds)
        {
            var userId = UserId;

            if (string.IsNullOrEmpty(userId))
                return;

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null || !conversation.HasParticipant(userId))
                return;

            conversation.HadVoiceCall = true;
            conversation.VoiceSeconds += Math.Max(0, durationSeconds);

            var user = await _context.Users.FindAsync(userId);
            if (user != null)
                user.TotalVoiceSeconds += Math.Max(0, durationSeconds);

            await _context.SaveChangesAsync();

            await Clients.OthersInGroup(conversationId.ToString()).SendAsync("VoiceCallEnded");
        }

        /// <summary>
        /// WebRTC signalling is chatty -- a single call trickles a dozen or
        /// more ICE candidates per side, and this used to hit the database
        /// on every one of them. Against a hosted Postgres that added
        /// hundreds of milliseconds to each candidate, delaying them enough
        /// that the ICE agent could give up before the good ones arrived.
        /// Conversation membership can't change while a connection is alive,
        /// so a successful check is cached for the life of the connection.
        /// </summary>
        private async Task<bool> IsActiveParticipant(Guid conversationId)
        {
            var userId = UserId;

            if (string.IsNullOrEmpty(userId))
                return false;

            var cacheKey = $"Participant:{conversationId}";

            if (Context.Items.ContainsKey(cacheKey))
                return true;

            var conversation = await _context.Conversations
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null || conversation.Status == ConversationStatus.Ended)
                return false;

            if (!conversation.HasParticipant(userId))
                throw new HubException("You are not a participant in this conversation.");

            Context.Items[cacheKey] = true;
            return true;
        }
    }
}
