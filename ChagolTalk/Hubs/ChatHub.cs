using ChagolTalk.Data;
using ChagolTalk.Interfaces;
using ChagolTalk.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ChagolTalk.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IMatchingService _matchingService;
        private readonly ApplicationDbContext _context;

        public ChatHub(
            IMatchingService matchingService,
            ApplicationDbContext context)
        {
            _matchingService = matchingService;
            _context = context;
        }

        // ==========================================
        // LOGOUT FROM CHAT
        // ==========================================

        public async Task LogoutFromChat(Guid conversationId)
        {
            var userId = Context.UserIdentifier;

            if (string.IsNullOrEmpty(userId))
                return;


            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);


            if (conversation == null)
                return;


            // Security check
            if (conversation.User1Id != userId &&
                conversation.User2Id != userId)
            {
                throw new HubException(
                    "You are not a participant in this conversation.");
            }


            // Already ended
            if (conversation.Status == ConversationStatus.Ended)
                return;


            // End conversation
            conversation.Status =
                ConversationStatus.Ended;

            conversation.EndedAt =
                DateTime.UtcNow;


            await _context.SaveChangesAsync();


            string groupName =
                conversationId.ToString();


            // Tell the OTHER user.
            await Clients.OthersInGroup(
                groupName)
                .SendAsync("ConversationEnded");


            // Remove this connection from the group.
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                groupName);


            Console.WriteLine(
                $"SERVER: User {userId} logged out from conversation {conversationId}");
        }

        // ==========================================
        // JOIN CONVERSATION
        // ==========================================

        public async Task JoinConversation(Guid conversationId)
        {
            var userId = Context.UserIdentifier;

            if (string.IsNullOrEmpty(userId))
                throw new HubException("User is not authenticated.");


            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);


            if (conversation == null)
                throw new HubException("Conversation not found.");


            // Make sure this user belongs to this conversation.
            if (conversation.User1Id != userId &&
                conversation.User2Id != userId)
            {
                throw new HubException(
                    "You are not a participant in this conversation.");
            }


            // Don't allow joining an already-ended conversation.
            if (conversation.Status == ConversationStatus.Ended)
            {
                throw new HubException(
                    "This conversation has already ended.");
            }


            string groupName = conversationId.ToString();


            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                groupName);


            Console.WriteLine(
                $"SERVER: {userId} joined conversation {conversationId}");


            await Clients.Caller.SendAsync(
                "JoinedConversation",
                conversationId);
        }


        // ==========================================
        // SEND MESSAGE
        // ==========================================

        public async Task SendMessage(
            Guid conversationId,
            string message)
        {
            var userId = Context.UserIdentifier;

            if (string.IsNullOrEmpty(userId))
                return;


            if (string.IsNullOrWhiteSpace(message))
                return;


            // Prevent extremely large messages.
            if (message.Length > 2000)
                return;


            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);


            if (conversation == null)
                return;


            // Security check.
            if (conversation.User1Id != userId &&
                conversation.User2Id != userId)
            {
                throw new HubException(
                    "You are not a participant in this conversation.");
            }


            // Don't allow messages after conversation ends.
            if (conversation.Status == ConversationStatus.Ended)
                return;


            string userName =
                Context.User?.Identity?.Name
                ?? "Stranger";


            string groupName =
                conversationId.ToString();


            Console.WriteLine(
                $"SERVER: Message from {userName}: {message}");


            // Send to everyone EXCEPT sender.
            await Clients.GroupExcept(
                groupName,
                new[] { Context.ConnectionId })
                .SendAsync(
                    "ReceiveMessage",
                    userName,
                    message);


            // Send back to sender.
            await Clients.Caller.SendAsync(
                "ReceiveOwnMessage",
                userName,
                message);
        }
        // ==========================================
        // START VOICE CALL
        // ==========================================

        public async Task StartVoiceCall(Guid conversationId)
        {
            var userId = Context.UserIdentifier;

            if (string.IsNullOrEmpty(userId))
                return;

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
                return;

            // Security check
            if (conversation.User1Id != userId &&
                conversation.User2Id != userId)
            {
                throw new HubException(
                    "You are not a participant in this conversation.");
            }

            // Don't allow calls after conversation ended.
            if (conversation.Status == ConversationStatus.Ended)
                return;

            string groupName = conversationId.ToString();

            // Tell the OTHER participant that a call is coming.
            await Clients.OthersInGroup(groupName)
                .SendAsync("IncomingVoiceCall");

            Console.WriteLine(
                $"VOICE CALL: {userId} started a call in {conversationId}");
        }

        // ==========================================
        // END CONVERSATION
        // ==========================================

        public async Task EndConversation(
            Guid conversationId)
        {
            var userId = Context.UserIdentifier;

            if (string.IsNullOrEmpty(userId))
                return;


            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);


            if (conversation == null)
                return;


            // Security check.
            if (conversation.User1Id != userId &&
                conversation.User2Id != userId)
            {
                throw new HubException(
                    "You are not a participant in this conversation.");
            }


            // Already ended.
            if (conversation.Status == ConversationStatus.Ended)
                return;


            // Mark conversation as ended.
            conversation.Status =
                ConversationStatus.Ended;

            conversation.EndedAt =
                DateTime.UtcNow;


            await _context.SaveChangesAsync();


            string groupName =
                conversationId.ToString();


            // Tell ONLY the other user.
            // The person who clicked End should NOT
            // receive the "other user left" notification.
            await Clients.OthersInGroup(
                groupName)
                .SendAsync(
                    "ConversationEnded");


            // Remove current user from the group.
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                groupName);


            Console.WriteLine(
                $"SERVER: Conversation {conversationId} ended by {userId}");
        }


        // ==========================================
        // FIND ANOTHER STRANGER
        // ==========================================

        public async Task FindAnother()
        {
            var userId = Context.UserIdentifier;

            if (string.IsNullOrEmpty(userId))
                return;


            var result = await _matchingService.FindMatchAsync(
                userId,
                Context.ConnectionId);


            // Nobody available yet.
            if (result.Conversation == null)
            {
                await Clients.Caller.SendAsync(
                    "WaitingForMatch");

                return;
            }


            var conversation =
                result.Conversation;


            var matchedConnectionId =
                result.MatchedConnectionId;


            // Save the new conversation.
            var existingConversation =
                await _context.Conversations
                    .FirstOrDefaultAsync(
                        c => c.Id == conversation.Id);


            if (existingConversation == null)
            {
                _context.Conversations.Add(
                    conversation);

                await _context.SaveChangesAsync();
            }


            // Tell current user.
            await Clients.Caller.SendAsync(
                "MatchFound",
                conversation.Id);


            // Tell matched user.
            if (!string.IsNullOrEmpty(matchedConnectionId))
            {
                await Clients.Client(
                    matchedConnectionId)
                    .SendAsync(
                        "MatchFound",
                        conversation.Id);
            }
        }


        // ==========================================
        // LEAVE CONVERSATION
        // ==========================================

        public async Task LeaveConversation(
            Guid conversationId)
        {
            string groupName =
                conversationId.ToString();


            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                groupName);


            Console.WriteLine(
                $"SERVER: {Context.UserIdentifier} left conversation {conversationId}");
        }


        // ==========================================
        // START MATCHING
        // ==========================================

        public async Task StartMatching()
        {
            var userId =
                Context.UserIdentifier;

            if (string.IsNullOrEmpty(userId))
                return;


            var result =
                await _matchingService.FindMatchAsync(
                    userId,
                    Context.ConnectionId);


            if (result.Conversation == null)
            {
                await Clients.Caller.SendAsync(
                    "WaitingForMatch");

                return;
            }


            var conversation =
                result.Conversation;


            var matchedConnectionId =
                result.MatchedConnectionId;


            // ======================================
            // SAVE CONVERSATION TO DATABASE
            // ======================================

            var existingConversation =
                await _context.Conversations
                    .FirstOrDefaultAsync(
                        c => c.Id == conversation.Id);


            if (existingConversation == null)
            {
                _context.Conversations.Add(
                    conversation);

                await _context.SaveChangesAsync();
            }


            // ======================================
            // NOTIFY BOTH USERS
            // ======================================

            await Clients.Caller.SendAsync(
                "MatchFound",
                conversation.Id);


            if (!string.IsNullOrEmpty(
                    matchedConnectionId))
            {
                await Clients.Client(
                    matchedConnectionId)
                    .SendAsync(
                        "MatchFound",
                        conversation.Id);
            }
        }


        // ==========================================
        // DISCONNECT
        // ==========================================

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId =
                Context.UserIdentifier;


            if (!string.IsNullOrEmpty(userId))
            {
                await _matchingService
                    .LeaveQueueAsync(userId);
            }


            await base.OnDisconnectedAsync(
                exception);
        }
        // ==========================================
        // WEBRTC OFFER
        // ==========================================

        public async Task SendVoiceOffer(
            Guid conversationId,
            string offer)
        {
            var userId = Context.UserIdentifier;

            if (string.IsNullOrEmpty(userId))
                return;

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null ||
                conversation.Status == ConversationStatus.Ended)
                return;

            if (conversation.User1Id != userId &&
                conversation.User2Id != userId)
            {
                throw new HubException(
                    "You are not a participant in this conversation.");
            }

            await Clients.OthersInGroup(
                conversationId.ToString())
                .SendAsync(
                    "ReceiveVoiceOffer",
                    offer);
        }


        // ==========================================
        // WEBRTC ANSWER
        // ==========================================

        public async Task SendVoiceAnswer(
            Guid conversationId,
            string answer)
        {
            var userId = Context.UserIdentifier;

            if (string.IsNullOrEmpty(userId))
                return;

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null ||
                conversation.Status == ConversationStatus.Ended)
                return;

            if (conversation.User1Id != userId &&
                conversation.User2Id != userId)
            {
                throw new HubException(
                    "You are not a participant in this conversation.");
            }

            await Clients.OthersInGroup(
                conversationId.ToString())
                .SendAsync(
                    "ReceiveVoiceAnswer",
                    answer);
        }


        // ==========================================
        // WEBRTC ICE CANDIDATE
        // ==========================================

        public async Task SendIceCandidate(
            Guid conversationId,
            string candidate)
        {
            var userId = Context.UserIdentifier;

            if (string.IsNullOrEmpty(userId))
                return;

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null ||
                conversation.Status == ConversationStatus.Ended)
                return;

            if (conversation.User1Id != userId &&
                conversation.User2Id != userId)
            {
                throw new HubException(
                    "You are not a participant in this conversation.");
            }

            await Clients.OthersInGroup(
                conversationId.ToString())
                .SendAsync(
                    "ReceiveIceCandidate",
                    candidate);
        }
        // ==========================================
        // END VOICE CALL
        // ==========================================

        public async Task EndVoiceCall(Guid conversationId)
        {
            var userId = Context.UserIdentifier;

            if (string.IsNullOrEmpty(userId))
                return;

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
                return;

            // Make sure the user belongs to this conversation.
            if (conversation.User1Id != userId &&
                conversation.User2Id != userId)
            {
                throw new HubException(
                    "You are not a participant in this conversation.");
            }

            // Tell the other person that the voice call ended.
            await Clients.OthersInGroup(
                conversationId.ToString())
                .SendAsync("VoiceCallEnded");
        }


    }
}