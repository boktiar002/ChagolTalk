using ChagolTalk.Hubs;
using ChagolTalk.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace ChagolTalk.Services
{
    /// <summary>
    /// Safety net for the matchmaking queue. Normally a user leaves the queue
    /// via OnDisconnectedAsync, but a hard crash / network drop can leave a
    /// ghost entry behind (SignalR's disconnect detection has a timeout).
    /// This periodically evicts anyone who has been waiting an unreasonably
    /// long time and tells their client to retry.
    /// </summary>
    public class QueueJanitorService : BackgroundService
    {
        private static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

        private readonly IMatchingService _matchingService;
        private readonly IHubContext<ChatHub> _hubContext;

        public QueueJanitorService(IMatchingService matchingService, IHubContext<ChatHub> hubContext)
        {
            _matchingService = matchingService;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);

            while (!stoppingToken.IsCancellationRequested &&
                   await timer.WaitForNextTickAsync(stoppingToken))
            {
                var stale = _matchingService.PruneStale(MaxWait);

                foreach (var user in stale)
                {
                    await _hubContext.Clients.Client(user.ConnectionId)
                        .SendAsync("MatchingTimedOut", cancellationToken: stoppingToken);
                }
            }
        }
    }
}
