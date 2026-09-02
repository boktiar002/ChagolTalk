using System.Diagnostics;
using ChagolTalk.Data;
using ChagolTalk.Interfaces;
using ChagolTalk.Models;
using ChagolTalk.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChagolTalk.Controllers
{
    public class HomeController : Controller
    {
        private readonly IPresenceTracker _presence;
        private readonly IMatchingService _matchingService;
        private readonly ApplicationDbContext _context;

        public HomeController(IPresenceTracker presence, IMatchingService matchingService, ApplicationDbContext context)
        {
            _presence = presence;
            _matchingService = matchingService;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.OnlineCount = _presence.OnlineCount;
            ViewBag.EstimatedWaitSeconds = (int)_matchingService.EstimatedWaitTime.TotalSeconds;
            ViewBag.PeopleTalking = await _context.Conversations
                .CountAsync(c => c.Status == ConversationStatus.Active) * 2;
            ViewBag.QuickStartError = TempData["QuickStartError"];
            ViewBag.QuickStartName = TempData["QuickStartName"];

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
