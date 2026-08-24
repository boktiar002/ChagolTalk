using ChagolTalk.Data;
using ChagolTalk.Interfaces;
using ChagolTalk.Models.Identity;
using ChagolTalk.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
        private readonly UserManager<ApplicationUser> _userManager;

        public ChatController(
            ApplicationDbContext context,
            IOptions<TurnServerOptions> turnOptions,
            IPresenceTracker presence,
            IMatchingService matchingService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _turnOptions = turnOptions.Value;
            _presence = presence;
            _matchingService = matchingService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Start()
        {
            ViewBag.OnlineCount = _presence.OnlineCount;
            ViewBag.WaitingCount = _matchingService.WaitingCount;

            var user = await _userManager.GetUserAsync(User);
            ViewBag.Interests = user?.Interests;
            ViewBag.Language = user?.Language;
            ViewBag.PreferredMode = user?.PreferredMode.ToString() ?? "Voice";

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
            if (conversation.Status == Models.Enums.ConversationStatus.Ended)
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
            var servers = new List<object>
            {
                new { urls = "stun:stun.l.google.com:19302" }
            };

            if (!string.IsNullOrWhiteSpace(_turnOptions.Url))
            {
                servers.Add(new
                {
                    urls = _turnOptions.Url,
                    username = _turnOptions.Username,
                    credential = _turnOptions.Credential
                });
            }

            return Json(new { iceServers = servers });
        }
    }
}
