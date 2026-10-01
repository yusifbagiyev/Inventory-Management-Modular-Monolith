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
using SharedServices.Auditing;
using SharedServices.Identity;
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
        private readonly AuditContext _audit;
        private readonly IAuditSink _auditLog;
        private readonly LoginThrottle _throttle;

        public AccountController(
            IdentityAuth identity,
            IUserManagementService userManagementService,
            ILogger<AccountController> logger,
            AuditContext audit,
            IAuditSink auditLog,
            LoginThrottle throttle)
        {
            _identity = identity;
            _userManagementService = userManagementService;
            _logger = logger;
            _audit = audit;
            _auditLog = auditLog;
            _throttle = throttle;
        }

        /// <summary>Issues the auth cookie; also after a password change, whose new stamp would otherwise end this session too.</summary>
        private async Task SignInAsync(IdentityService.Application.DTOs.UserDto user, bool persistent, DateTimeOffset? signedInAt = null)
        {
            var stamp = await _identity.GetSessionStampAsync(user.Id);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                UserPrincipalFactory.Create(user, stamp, signedInAt ?? DateTimeOffset.UtcNow),
                new AuthenticationProperties
                {
                    IsPersistent = persistent,
                    ExpiresUtc = persistent ? DateTimeOffset.UtcNow.AddDays(30) : null,
                    AllowRefresh = true
                });
        }

        /// <summary>Sign-in events in the audit log ("Session" rows).</summary>
        private Task AuditSessionAsync(string operation, int? userId, string? userName, string? username, string? reason = null)
            => _auditLog.WriteAsync([_audit.Record("Session", userId?.ToString(), username, operation,
                reason == null ? null : [new AuditFieldChange("Reason", null, reason)], userId, userName)]);

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(IdentityModule.LoginRateLimitPolicy)]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid)
                return View(model);

            var address = HttpContext.Connection.RemoteIpAddress?.ToString();
            if (_throttle.RetryAfter(address) is { } wait)
            {
                ModelState.AddModelError(string.Empty, JsonStringLocalizer.TranslateMessage(
                    $"Too many failed sign-ins from your network. Try again in {(int)Math.Ceiling(wait.TotalMinutes)} minutes."));
                return View(model);
            }

            try
            {
                var user = await _identity.ValidateCredentialsAsync(model.Username, model.Password);
                await SignInAsync(user, model.RememberMe);

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
                var fullName = $"{user.FirstName} {user.LastName}".Trim();
                await AuditSessionAsync(AuditOperations.SignedIn, user.Id,
                    fullName.Length > 0 ? $"{fullName} ({user.Username})" : user.Username, user.Username);

                return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                    ? Redirect(returnUrl)
                    : RedirectToAction("Index", "Home");
            }
            catch (UnauthorizedAccessException ex)
            {
                _throttle.RecordFailure(address);
                _logger.LogWarning("Failed sign-in for {Username} from {Ip}: {Reason}",
                    model.Username, HttpContext.Connection.RemoteIpAddress, ex.Message);
                await AuditSessionAsync(AuditOperations.SignInFailed, null, model.Username, model.Username, ex.Message);
                // One answer for every failure (unknown user, wrong password, locked account), so
                // it cannot be used to find usernames.
                ModelState.AddModelError(string.Empty, JsonStringLocalizer.TranslateMessage(
                    "Invalid username or password. After repeated wrong attempts, sign-in is paused for 15 minutes."));
                return View(model);
            }
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Recorded only for a signed-in user: anyone can post here, and each call wrote a row.
            if (User.Identity?.IsAuthenticated == true)
            {
                _logger.LogInformation("User {Username} signed out", User.Identity?.Name);
                await AuditSessionAsync(AuditOperations.SignedOut, _audit.UserId, _audit.UserName, User.Identity?.Name);
            }
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        [AllowAnonymous]
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
                // The new password ends the user's other sessions (security stamp); this one is
                // issued again so it stays.
                var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                var me = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var myId)
                    ? await _identity.GetUserAsync(myId) : null;
                if (me != null)
                    await SignInAsync(me, auth.Properties?.IsPersistent == true, UserPrincipalFactory.SignedInAt(User));

                TempData["Success"] = JsonStringLocalizer.TranslateMessage("Your password has been changed.");
                return RedirectToAction(nameof(Profile));
            }

            ModelState.AddModelError(string.Empty, JsonStringLocalizer.TranslateMessage(error ?? "Could not change the password."));
            return View(model);
        }
    }
}
