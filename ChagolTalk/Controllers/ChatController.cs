using ChagolTalk.Data;
using ChagolTalk.Hubs;
using ChagolTalk.Interfaces;
using ChagolTalk.Models.Enums;
using ChagolTalk.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChagolTalk.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly TurnServerOptions _turnOptions;
        private readonly IPresenceTracker _presence;
        private readonly IMatchingService _matchingService;
        private readonly IHubContext<ChatHub> _hubContext;

        public ChatController(
            ApplicationDbContext context,
            IOptions<TurnServerOptions> turnOptions,
            IPresenceTracker presence,
            IMatchingService matchingService,
            IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _turnOptions = turnOptions.Value;
            _presence = presence;
            _matchingService = matchingService;
            _hubContext = hubContext;
        }

        public async Task<IActionResult> Start(int? auto)
        {
            ViewBag.OnlineCount = _presence.OnlineCount;
            ViewBag.EstimatedWaitSeconds = (int)_matchingService.EstimatedWaitTime.TotalSeconds;
            ViewBag.PeopleTalking = await _context.Conversations
                .CountAsync(c => c.Status == ConversationStatus.Active) * 2;
            ViewBag.AutoStart = auto == 1;

            return View();
        }

        public async Task<IActionResult> Room(Guid id)
        {
            var userId = User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier
            )?.Value;

            if (string.IsNullOrEmpty(userId))
                return Challenge();

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == id);

            if (conversation == null)
                return NotFound();

            // Make sure the logged-in user belongs to this conversation.
            if (conversation.User1Id != userId &&
                conversation.User2Id != userId)
            {
                return Forbid();
            }

            // Don't open ended conversations.
            if (conversation.Status == ConversationStatus.Ended)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            ViewBag.ConversationId = conversation.Id;
            ViewBag.Mode = conversation.Mode.ToString();

            return View();
        }

        /// <summary>
        /// ICE server list for the WebRTC peer connection. Kept server-side
        /// so TURN credentials never need to live in client-side source and
        /// can be rotated purely via configuration/environment variables.
        /// </summary>
        [HttpGet]
        public IActionResult IceServers()
        {
            // Several STUN servers, because a single unreachable one costs
            // the browser a retransmit cycle before it moves on.
            var servers = new List<object>
            {
                new { urls = "stun:stun.l.google.com:19302" },
                new { urls = "stun:stun1.l.google.com:19302" },
                new { urls = "stun:stun.cloudflare.com:3478" }
            };

            var turnUrls = (_turnOptions.Urls ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var url in turnUrls)
            {
                servers.Add(new
                {
                    urls = url,
                    username = _turnOptions.Username,
                    credential = _turnOptions.Credential
                });
            }

            return Json(new { iceServers = servers });
        }

        /// <summary>
        /// Fired via navigator.sendBeacon when the tab closes/navigates away
        /// mid-conversation. beforeunload can't reliably await a SignalR
        /// invoke before the page dies, so this is a plain HTTP fallback
        /// that ends the conversation the same way the hub does.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LeaveOnUnload(Guid id)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
                return Ok();

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == id);

            if (conversation == null ||
                conversation.Status == ConversationStatus.Ended ||
                (conversation.User1Id != userId && conversation.User2Id != userId))
            {
                return Ok();
            }

            conversation.Status = ConversationStatus.Ended;
            conversation.EndedAt = DateTime.UtcNow;
            conversation.EndedByUserId = userId;

            await _context.SaveChangesAsync();

            await _hubContext.Clients.Group(id.ToString()).SendAsync("ConversationEnded");

            return Ok();
        }
    }
}
