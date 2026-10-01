using IdentityService.API;
using InventoryManagement.Web.Localization;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using IdentityAuth = IdentityService.Application.Services.IAuthService;

namespace InventoryManagement.Web.Controllers
{
    public class AccountController : Controller
    {
        /// <summary>Non-HttpOnly convenience cookie the login page uses to prefill the username.</summary>
        private const string RememberedUsernameCookie = "username";

        private readonly IdentityAuth _identity;
        private readonly IUserManagementService _userManagementService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            IdentityAuth identity,
            IUserManagementService userManagementService,
            ILogger<AccountController> logger)
        {
            _identity = identity;
            _userManagementService = userManagementService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(IdentityModule.LoginRateLimitPolicy)]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var user = await _identity.ValidateCredentialsAsync(model.Username, model.Password);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    UserPrincipalFactory.Create(user),
                    new AuthenticationProperties
                    {
                        IsPersistent = model.RememberMe,
                        ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : null,
                        AllowRefresh = true
                    });

                if (model.RememberMe)
                {
                    Response.Cookies.Append(RememberedUsernameCookie, model.Username, new CookieOptions
                    {
                        Secure = Request.IsHttps,
                        SameSite = SameSiteMode.Strict,
                        Expires = DateTimeOffset.UtcNow.AddDays(365)
                    });
                }
                else
                {
                    Response.Cookies.Delete(RememberedUsernameCookie);
                }

                _logger.LogInformation("User {Username} signed in from {Ip}", model.Username, HttpContext.Connection.RemoteIpAddress);

                return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                    ? Redirect(returnUrl)
                    : RedirectToAction("Index", "Home");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Failed sign-in for {Username} from {Ip}: {Reason}",
                    model.Username, HttpContext.Connection.RemoteIpAddress, ex.Message);
                ModelState.AddModelError(string.Empty, ex.Message.StartsWith("Account is locked")
                    ? JsonStringLocalizer.TranslateMessage(ex.Message)
                    : JsonStringLocalizer.TranslateMessage("Invalid username or password."));
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            _logger.LogInformation("User {Username} signed out", User.Identity?.Name);
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        public IActionResult AccessDenied() => View();

        /// <summary>Session check for the client-side monitor: 200 while signed in, 401 otherwise.</summary>
        [Authorize]
        [HttpGet]
        public IActionResult Ping() => NoContent();

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
            var profile = await _userManagementService.GetUserProfileAsync(userId);
            return profile == null ? RedirectToAction(nameof(Login)) : View(profile);
        }

        // Self-service password change for the signed-in user (any authenticated role).
        // Admins set other users' passwords from User Management.
        [Authorize]
        [HttpGet]
        public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var (success, error) = await _userManagementService.ChangePasswordAsync(model.CurrentPassword, model.NewPassword);
            if (success)
            {
                TempData["Success"] = JsonStringLocalizer.TranslateMessage("Your password has been changed.");
                return RedirectToAction(nameof(Profile));
            }

            ModelState.AddModelError(string.Empty, JsonStringLocalizer.TranslateMessage(error ?? "Could not change the password."));
            return View(model);
        }
    }
}
