using ChagolTalk.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ChagolTalk.Controllers
{
    public class HomeController : Controller
    {
        private readonly IPresenceTracker _presence;

        public HomeController(IPresenceTracker presence)
        {
            _presence = presence;
        }

        public IActionResult Index()
        {
            ViewBag.OnlineCount = _presence.OnlineCount;

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}
