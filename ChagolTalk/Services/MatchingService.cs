using System.Collections.Concurrent;
using ChagolTalk.Interfaces;
using ChagolTalk.Models.Entities;
using ChagolTalk.Models.Enums;
using ChagolTalk.Models.Realtime;

namespace ChagolTalk.Services
{
    public class MatchingService : IMatchingService
    {
        private static readonly ConcurrentQueue<WaitingUser> WaitingUsers = new();

        public Task<(Conversation? Conversation, string? MatchedConnectionId)>
            FindMatchAsync(string userId, string connectionId)
        {
            Console.WriteLine("========== MATCHING SERVICE ==========");
            Console.WriteLine($"Incoming UserId: {userId}");
            Console.WriteLine($"Incoming ConnectionId: {connectionId}");
            Console.WriteLine($"Queue count BEFORE: {WaitingUsers.Count}");

            // Prevent the same user from entering the queue twice.
            if (WaitingUsers.Any(x => x.UserId == userId))
            {
                Console.WriteLine("User is already waiting in the queue.");

                return Task.FromResult(
                    ((Conversation?)null, (string?)null));
            }

            // Look for another waiting user.
            while (WaitingUsers.TryDequeue(out var otherUser))
            {
                Console.WriteLine($"Dequeued UserId: {otherUser.UserId}");
                Console.WriteLine($"Dequeued ConnectionId: {otherUser.ConnectionId}");

                // Never match a user with themselves.
                if (otherUser.UserId == userId)
                {
                    Console.WriteLine("Dequeued user is the same user. Skipping.");
                    continue;
                }

                // Create the conversation.
                var conversation = new Conversation
                {
                    Id = Guid.NewGuid(),

                    // IMPORTANT:
                    // Keeping your User1Id/User2Id exactly as they are.
                    User1Id = otherUser.UserId,
                    User2Id = userId,

                    StartedAt = DateTime.UtcNow,
                    Status = ConversationStatus.Active
                };

                Console.WriteLine("******** MATCH FOUND ********");
                Console.WriteLine($"Conversation ID: {conversation.Id}");
                Console.WriteLine($"User 1: {conversation.User1Id}");
                Console.WriteLine($"User 2: {conversation.User2Id}");
                Console.WriteLine(
                    $"Matched Connection: {otherUser.ConnectionId}");

                return Task.FromResult(
                    (
                        (Conversation?)conversation,
                        (string?)otherUser.ConnectionId
                    ));
            }

            // Nobody was waiting, so add this user to the queue.
            Console.WriteLine("No waiting user found.");
            Console.WriteLine("Adding current user to queue.");

            WaitingUsers.Enqueue(new WaitingUser
            {
                UserId = userId,
                ConnectionId = connectionId,
                JoinedAt = DateTime.UtcNow
            });

            Console.WriteLine($"Queue count AFTER: {WaitingUsers.Count}");
            Console.WriteLine("======================================");

            return Task.FromResult(
                ((Conversation?)null, (string?)null));
        }

        public Task LeaveQueueAsync(string userId)
        {
            Console.WriteLine($"Removing UserId from queue: {userId}");

            var remainingUsers = new List<WaitingUser>();

            while (WaitingUsers.TryDequeue(out var waitingUser))
            {
                if (waitingUser.UserId != userId)
                {
                    remainingUsers.Add(waitingUser);
                }
            }

            foreach (var waitingUser in remainingUsers)
            {
                WaitingUsers.Enqueue(waitingUser);
            }

            Console.WriteLine(
                $"Queue count after removal: {WaitingUsers.Count}");

            return Task.CompletedTask;
        }

        public bool IsWaiting(string userId)
        {
            return WaitingUsers.Any(x => x.UserId == userId);
        }
    }
}