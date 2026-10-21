using Microsoft.AspNetCore.Authorization;

namespace Inventory_Management_System.Controllers
{
    /// <summary>
    /// Dedicated controller for switching the application UI culture.
    /// Persists the selected culture in a long-lived cookie so the choice
    /// survives page refreshes and browser restarts.
    /// </summary>
    [AllowAnonymous]
    public class CultureController : Controller
    {
        private static readonly HashSet<string> _supportedCultures =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "en", "ar", "fr", "es", "it", "de"
            };

        // POST /Culture/SetLanguage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetLanguage(string culture, string returnUrl)
        {
            // Guard: only accept cultures we actually support
            if (!_supportedCultures.Contains(culture))
            {
                culture = "en";
            }

            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions
                {
                    Expires    = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true,
                    SameSite   = SameSiteMode.Lax
                }
            );

            // Safe local redirect only
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return LocalRedirect("~/");
        }
    }
}
