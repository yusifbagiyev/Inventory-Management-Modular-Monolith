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
    /// <summary>Sign-in, sign-out, the profile page and the user's own password change.</summary>
    public class AccountController : Controller
    {
        // Old username cookie that sign-in deletes
        private const string OldUsernameCookie = "username";

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

        /// <summary>Issues the auth cookie, also after a password change so the new stamp does not end this session.</summary>
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

        private Task AuditSessionAsync(string operation, int? userId, string? userName, string? username, string? reason = null)
            => _auditLog.WriteAsync([_audit.Record("Session", userId?.ToString(), username, operation,
                reason == null ? null : [new AuditFieldChange("Reason", null, reason)], userId, userName)]);

        /// <summary>Sign-in in two steps, picking an account remembered on this browser and then entering the password.</summary>
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null, string? user = null, int? other = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            var model = new LoginViewModel { ReturnUrl = returnUrl };
            var recent = RecentAccounts.Read(Request);
            if (user != null && recent.Any(a => string.Equals(a.Login, user, StringComparison.OrdinalIgnoreCase)))
            {
                model.Mode = "user";
                model.Username = recent.First(a => string.Equals(a.Login, user, StringComparison.OrdinalIgnoreCase)).Login;
            }
            else if (user != null || other == 1 || recent.Count == 0)
                model.Mode = "other";
            else
                model.ShowPicker = true;

            return View(Prepare(model));
        }

        /// <summary>Fills the account list and the chosen account's name for any step.</summary>
        private LoginViewModel Prepare(LoginViewModel model)
        {
            ViewData["ReturnUrl"] = model.ReturnUrl;
            var recent = RecentAccounts.Read(Request);
            model.Recent = recent.Select(a => new RecentAccountView(a.Login, a.Name,
                JsonStringLocalizer.TranslateMessage(a.Role == SharedServices.Identity.AllRoles.Admin ? "Administrator" : "User"),
                RecentAccounts.LastAtText(a.LastAt), RecentAccounts.Initials(a.Name))).ToList();

            var chosen = model.Mode == "user"
                ? recent.FirstOrDefault(a => string.Equals(a.Login, model.Username, StringComparison.OrdinalIgnoreCase))
                : null;
            if (model.Mode == "user" && chosen == null)
                model.Mode = "other";   // Forgotten in the meantime, so ask for the username

            if (chosen != null)
            {
                model.DisplayName = chosen.Name;
                model.LoginHint = chosen.Login;
                model.Initials = RecentAccounts.Initials(chosen.Name);
            }
            else if (!model.ShowPicker)
                model.Mode = "other";
            return model;
        }

        /// <summary>Removes an account from this browser's list, for shared computers.</summary>
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ForgetAccount(string login, string? returnUrl = null)
        {
            RecentAccounts.Forget(HttpContext, login);
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(IdentityModule.LoginRateLimitPolicy)]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            model.ReturnUrl ??= returnUrl;
            returnUrl = model.ReturnUrl;
            if (!ModelState.IsValid)
                return View(Prepare(model));

            var address = HttpContext.Connection.RemoteIpAddress?.ToString();
            if (_throttle.RetryAfter(address) is { } wait)
            {
                ModelState.AddModelError(string.Empty, JsonStringLocalizer.TranslateMessage(
                    $"Too many failed sign-ins from your network. Try again in {(int)Math.Ceiling(wait.TotalMinutes)} minutes."));
                return View(Prepare(model));
            }

            try
            {
                var user = await _identity.ValidateCredentialsAsync(model.Username, model.Password);
                // Session cookie that ends with the browser, while the account stays in the picker list
                await SignInAsync(user, persistent: false);
                var displayName = $"{user.FirstName} {user.LastName}".Trim();
                RecentAccounts.Remember(HttpContext, new RecentAccounts.Entry(user.Username,
                    displayName.Length > 0 ? displayName : user.Username,
                    user.Roles.Contains(SharedServices.Identity.AllRoles.Admin) ? SharedServices.Identity.AllRoles.Admin : SharedServices.Identity.AllRoles.User,
                    DateTime.UtcNow));
                Response.Cookies.Delete(OldUsernameCookie);

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
                // Same answer for every failure so it cannot be used to probe usernames
                ModelState.AddModelError(string.Empty, JsonStringLocalizer.TranslateMessage(
                    "Invalid username or password. After repeated failed attempts, sign-in is suspended for 15 minutes."));
                return View(Prepare(model));
            }
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Anyone can post here, so only a signed-in user's sign-out is audited
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

        /// <summary>Lets the client-side session monitor see whether it is still signed in.</summary>
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

        /// <summary>Own password only, Admins set other users' passwords from User Management.</summary>
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
                // The new stamp ends the other sessions, so re-issue this one to keep it alive
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
