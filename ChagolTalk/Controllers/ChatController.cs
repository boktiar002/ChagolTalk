using ChagolTalk.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChagolTalk.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ChatController(ApplicationDbContext context)
        {
            _context = context;
        }


        public IActionResult Start()
        {
            return View();
        }


        public async Task<IActionResult> Room(Guid id)
        {
            var userId = User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier
            )?.Value;


            if (string.IsNullOrEmpty(userId))
                return Challenge();


            var conversation =
                await _context.Conversations
                    .FirstOrDefaultAsync(
                        c => c.Id == id);


            if (conversation == null)
                return NotFound();


            // Make sure the logged-in user belongs
            // to this conversation.
            if (conversation.User1Id != userId &&
                conversation.User2Id != userId)
            {
                return Forbid();
            }


            // Don't open ended conversations.
            if (conversation.Status ==
                Models.Enums.ConversationStatus.Ended)
            {
                return RedirectToAction(
                    "Index",
                    "Dashboard");
            }


            ViewBag.ConversationId =
                conversation.Id;


            return View();
        }
    }
}