using InventoryManagement.Web.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Web.Controllers
{
    /// <summary>Switches the interface language (a cookie read by the request-localization middleware).</summary>
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

        /// <summary>
        /// The translation table for scripts (<c>t()</c> in site.js), as <c>window.I18n</c>. The layout
        /// requests it with <c>?lang=</c> and <c>?v=</c> the tables' hash, so it can be cached for good: the
        /// table follows <c>lang</c> (not the culture cookie), so a cached copy always matches its URL.
        /// </summary>
        [HttpGet]
        [ResponseCache(Duration = 31536000, Location = ResponseCacheLocation.Any)]
        public IActionResult Strings(string? lang)
        {
            // Served as its own script file (never inlined in HTML), so readable UTF-8 is safe and much smaller.
            var table = lang == null ? JsonStringLocalizer.CurrentTable : JsonStringLocalizer.TableFor(lang);
            var json = System.Text.Json.JsonSerializer.Serialize(table,
                new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            return Content($"window.I18n = {json};", "text/javascript; charset=utf-8");
        }
    }
}
