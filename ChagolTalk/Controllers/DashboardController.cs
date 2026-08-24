using ChagolTalk.Data;
using ChagolTalk.Interfaces;
using ChagolTalk.Models.Identity;
using ChagolTalk.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChagolTalk.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IMatchingService _matchingService;
        private readonly IPresenceTracker _presence;

        public DashboardController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IMatchingService matchingService,
            IPresenceTracker presence)
        {
            _userManager = userManager;
            _context = context;
            _matchingService = matchingService;
            _presence = presence;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            var recentCount = await _context.Conversations
                .Where(c => c.User1Id == user.Id || c.User2Id == user.Id)
                .CountAsync();

            var model = new DashboardViewModel
            {
                User = user,
                OnlineCount = _presence.OnlineCount,
                WaitingCount = _matchingService.WaitingCount,
                RecentConversationCount = recentCount
            };

            return View(model);
        }
    }
}
