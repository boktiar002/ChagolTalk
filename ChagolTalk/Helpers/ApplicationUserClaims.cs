using System.Security.Claims;
using ChagolTalk.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ChagolTalk.Helpers
{
    /// <summary>
    /// Puts the two things the shared layout needs -- the display name and
    /// whether this is a guest session -- into the auth cookie.
    ///
    /// The layout used to call UserManager.GetUserAsync on every render, which
    /// meant a round trip to Postgres to draw the navbar on every single page
    /// view. Neither value changes often, and both are already known at
    /// sign-in, so they travel in the cookie instead.
    /// </summary>
    public class ApplicationUserClaimsPrincipalFactory
        : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
    {
        public const string DisplayNameClaim = "ct:display_name";
        public const string IsGuestClaim = "ct:is_guest";

        public ApplicationUserClaimsPrincipalFactory(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> options)
            : base(userManager, roleManager, options)
        {
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);

            identity.AddClaim(new Claim(
                DisplayNameClaim,
                user.DisplayName ?? user.UserName ?? "Stranger"));

            identity.AddClaim(new Claim(IsGuestClaim, user.IsGuest ? "1" : "0"));

            return identity;
        }
    }

    public static class ClaimsPrincipalExtensions
    {
        /// <summary>
        /// Display name from the cookie, or null when the cookie predates
        /// these claims. Callers fall back to the database for that case so
        /// sessions issued before this change keep working until they expire.
        /// </summary>
        public static string? DisplayNameOrNull(this ClaimsPrincipal user) =>
            user.FindFirst(ApplicationUserClaimsPrincipalFactory.DisplayNameClaim)?.Value;

        /// <inheritdoc cref="DisplayNameOrNull"/>
        public static bool? IsGuestOrNull(this ClaimsPrincipal user) =>
            user.FindFirst(ApplicationUserClaimsPrincipalFactory.IsGuestClaim)?.Value switch
            {
                "1" => true,
                "0" => false,
                _ => null,
            };
    }
}
