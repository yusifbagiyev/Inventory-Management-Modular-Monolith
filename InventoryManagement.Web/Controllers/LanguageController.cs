using InventoryManagement.Web.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Web.Controllers
{
    /// <summary>Switches the UI language through the culture cookie.</summary>
    [AllowAnonymous]
    public class LanguageController : Controller
    {
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Set(string culture, string? returnUrl)
        {
            if (!LocalizationSetup.UiCultures.Contains(culture))
                culture = LocalizationSetup.DefaultUiCulture;

            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                LocalizationSetup.CookieValue(culture),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true,
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps
                });

            return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
        }

        // The table follows lang rather than the cookie, so a cached copy always matches its URL and can be kept for a year.
        /// <summary>Translation table for scripts as window.I18n.</summary>
        [HttpGet]
        [ResponseCache(Duration = 31536000, Location = ResponseCacheLocation.Any)]
        public IActionResult Strings(string? lang)
        {
            // Relaxed escaping is safe because this is never inlined in HTML, and it is much smaller.
            var table = lang == null ? JsonStringLocalizer.CurrentTable : JsonStringLocalizer.TableFor(lang);
            var json = System.Text.Json.JsonSerializer.Serialize(table,
                new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            return Content($"window.I18n = {json};", "text/javascript; charset=utf-8");
        }
    }
}
