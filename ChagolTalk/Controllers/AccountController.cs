using ChagolTalk.Models.Identity;
using Microsoft.AspNetCore.Authorization;
using ChagolTalk.ViewModels.Account;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ChagolTalk.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // GET: /Account/Register
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = new ApplicationUser
            {
                UserName = model.Username,
                DisplayName = model.Username,
                AvatarSeed = Guid.NewGuid().ToString("N")[..8],
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: false);

                return RedirectToAction("Index", "Dashboard");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var result = await _signInManager.PasswordSignInAsync(
                model.Username,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: false);

            if (result.Succeeded)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError("", "Too many failed attempts. Try again later.");
                return View(model);
            }

            ModelState.AddModelError("", "Invalid username or password.");

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();

            return RedirectToAction("Index", "Home");
        }

        // POST: /Account/QuickStart
        // Lets someone start a voice call with nothing but a display name --
        // no password, no email. Creates a lightweight guest account behind
        // the scenes and signs them in the same way a registered user would
        // be, so the rest of the app (hub auth, conversations, dashboard)
        // doesn't need to know the difference.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickStart(QuickStartViewModel model)
        {
            var name = (model.DisplayName ?? string.Empty).Trim();

            if (name.Length < 2)
            {
                TempData["QuickStartError"] = "Enter a name with at least 2 characters.";
                return RedirectToAction("Index", "Home");
            }

            if (name.Length > 20)
                name = name[..20];

            var user = new ApplicationUser
            {
                UserName = "guest_" + Guid.NewGuid().ToString("N")[..12],
                DisplayName = name,
                IsGuest = true,
                AvatarSeed = Guid.NewGuid().ToString("N")[..8],
                PreferredMode = ChagolTalk.Models.Enums.ChatMode.Voice,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user);

            if (!result.Succeeded)
            {
                TempData["QuickStartError"] = "Could not start a session. Please try again.";
                return RedirectToAction("Index", "Home");
            }

            await _signInManager.SignInAsync(user, isPersistent: false);

            return RedirectToAction("Start", "Chat", new { auto = 1 });
        }

        // GET: /Account/EditProfile
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> EditProfile()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            var model = new EditProfileViewModel
            {
                DisplayName = user.DisplayName,
                Bio = user.Bio,
                Country = user.Country,
                DateOfBirth = user.DateOfBirth,
                Interests = user.Interests,
                Language = user.Language,
                PreferredMode = user.PreferredMode,
                AvatarSeed = user.AvatarSeed
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> EditProfile(EditProfileViewModel model)
        {
            if (model.DateOfBirth.HasValue)
            {
                var age = DateTime.UtcNow.Year - model.DateOfBirth.Value.Year;
                if (model.DateOfBirth.Value.Date > DateTime.UtcNow.AddYears(-age)) age--;

                if (age < 13)
                {
                    ModelState.AddModelError(nameof(model.DateOfBirth), "You must be at least 13 years old to use ChagolTalk.");
                }
            }

            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            user.DisplayName = string.IsNullOrWhiteSpace(model.DisplayName) ? user.DisplayName : model.DisplayName.Trim();
            user.Bio = model.Bio?.Trim();
            user.Country = model.Country?.Trim();
            user.DateOfBirth = model.DateOfBirth;
            user.Language = string.IsNullOrWhiteSpace(model.Language) ? null : model.Language.Trim();
            user.PreferredMode = model.PreferredMode;
            user.UpdatedAt = DateTime.UtcNow;

            user.Interests = string.IsNullOrWhiteSpace(model.Interests)
                ? null
                : string.Join(",", model.Interests
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(i => i.ToLowerInvariant())
                    .Distinct()
                    .Take(10));

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                return View(model);
            }

            TempData["ProfileSaved"] = true;

            return RedirectToAction(nameof(EditProfile));
        }

        // POST: /Account/RerollAvatar
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> RerollAvatar()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            user.AvatarSeed = Guid.NewGuid().ToString("N")[..8];
            user.UpdatedAt = DateTime.UtcNow;

            await _userManager.UpdateAsync(user);

            return RedirectToAction(nameof(EditProfile));
        }
    }
}
