using Microsoft.AspNetCore.Mvc;

namespace ChagolTalk.Controllers
{
    public class ChatController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
