using System.Security.Claims;
using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace InventoryManagement.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAuthService _authService;
        private readonly ITokenManager _tokenManager;
        private readonly ILogger<AccountController> _logger;
        public AccountController(
            IAuthService authService,
            ILogger<AccountController> logger,
            ITokenManager tokenManager)
        {
            _authService = authService;
            _logger = logger;
            _tokenManager = tokenManager;
        }


        [HttpGet]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated ?? false)
            {
                var token = HttpContext.Session.GetString("JwtToken");
                if (!string.IsNullOrEmpty(token))
                {
                    var validToken = await _tokenManager.GetValidTokenAsync();
                    if (!string.IsNullOrEmpty(validToken))
                    {
                        return RedirectToAction("Index", "Home");
                    }
                }

                // If we get here, token is invalid - clean up properly
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                HttpContext.Session.Clear();
                Response.Cookies.Delete("jwt_token");
                Response.Cookies.Delete("refresh_token");
                Response.Cookies.Delete("user_data");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // Perform login
                var result = await _authService.LoginAsync(model.Username, model.Password);

                if (result != null && !string.IsNullOrEmpty(result.AccessToken))
                {
                    // CRITICAL: Store tokens ONLY in server-side session for security
                    HttpContext.Session.SetString("JwtToken", result.AccessToken);
                    HttpContext.Session.SetString("RefreshToken", result.RefreshToken);
                    HttpContext.Session.SetString("UserData", JsonConvert.SerializeObject(result.User));

                    // Generate token version for rotation tracking
                    var tokenVersion = Guid.NewGuid().ToString();
                    HttpContext.Session.SetString("TokenVersion", tokenVersion);

                    // Set version cookie (HttpOnly) for client-side version checking
                    var versionCookieOptions = new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = HttpContext.Request.IsHttps,
                        SameSite = SameSiteMode.Strict, // Strict for CSRF protection
                        Expires = DateTimeOffset.UtcNow.AddDays(1)
                    };
                    Response.Cookies.Append("token_version", tokenVersion, versionCookieOptions);

                    // Create authentication cookie with claims
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, result.User.Id.ToString()),
                        new Claim(ClaimTypes.Name, result.User.Username),
                        new Claim(ClaimTypes.Email, result.User.Email),
                        new Claim("FirstName", result.User.FirstName),
                        new Claim("LastName", result.User.LastName)
                    };

                    foreach (var role in result.User.Roles)
                    {
                        claims.Add(new Claim(ClaimTypes.Role, role));
                    }

                    foreach (var permission in result.User.Permissions)
                    {
                        claims.Add(new Claim("permission", permission));
                    }

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                    // Set authentication properties based on Remember Me
                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = model.RememberMe,
                        ExpiresUtc = model.RememberMe ?
                            DateTimeOffset.UtcNow.AddDays(7) : // 7 days for Remember Me
                            DateTimeOffset.UtcNow.AddHours(8), // 8 hours for normal session
                        AllowRefresh = true
                    };

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties);

                    _logger.LogInformation("User {Username} logged in successfully from IP {IP}",
                        model.Username, HttpContext.Connection.RemoteIpAddress);

                    // Validate and sanitize return URL to prevent open redirect attacks
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }

                    return RedirectToAction("Dashboard", "Home");
                }

                _logger.LogWarning("Failed login attempt for username: {Username} from IP {IP}",
                    model.Username, HttpContext.Connection.RemoteIpAddress);

                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error for username: {Username}", model.Username);
                ModelState.AddModelError(string.Empty, "An error occurred during login. Please try again.");
            }

            return View(model);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var username = User.Identity?.Name;

            try
            {
                // Clear server-side session completely
                HttpContext.Session.Clear();

                // Sign out from cookie authentication
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                // Clear ALL authentication-related cookies
                var cookiesToClear = new[]
                {
                    "token_version",
                    ".AspNetCore.Session",
                    ".InventoryManagement.Session",
                    ".AspNetCore.Cookies"
                };

                foreach (var cookieName in cookiesToClear)
                {
                    if (Request.Cookies.ContainsKey(cookieName))
                    {
                        Response.Cookies.Delete(cookieName, new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = Request.IsHttps,
                            SameSite = SameSiteMode.Strict,
                            Path = "/" // Ensure cookie is deleted from root path
                        });
                    }
                }

                _logger.LogInformation("User {Username} logged out successfully", username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout for user {Username}", username);
            }

            // Always redirect to login even if logout had issues
            return RedirectToAction("Login", "Account");
        }



        public IActionResult AccessDenied()
        {
            return View();
        }



        [HttpGet]
        public IActionResult Profile()
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return RedirectToAction("Login");
            }

            var userDataJson=HttpContext.Session.GetString("UserData");
            if(string.IsNullOrEmpty(userDataJson))
            {
                return RedirectToAction("Login");
            }

            var userData = JsonConvert.DeserializeObject<User>(userDataJson);
            return View(userData);
        }



        [HttpGet]
        public async Task<IActionResult> RefreshToken()
        {
            var accessToken = HttpContext.Session.GetString("JwtToken")
                ?? Request.Cookies["jwt_token"];
            var refreshToken = HttpContext.Session.GetString("RefreshToken")
                ?? Request.Cookies["refresh_token"];

            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "No tokens available" });
                }
                return RedirectToAction("Login");
            }

            try
            {
                var result = await _authService.RefreshTokenAsync(accessToken, refreshToken);

                if (result != null)
                {
                    // Update all storage locations
                    HttpContext.Session.SetString("JwtToken", result.AccessToken);
                    HttpContext.Session.SetString("RefreshToken", result.RefreshToken);
                    HttpContext.Session.SetString("UserData", JsonConvert.SerializeObject(result.User));

                    // Update cookies if they exist
                    if (Request.Cookies.ContainsKey("jwt_token"))
                    {
                        var cookieOptions = new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = Request.IsHttps,
                            SameSite = SameSiteMode.Lax,
                            Expires = DateTimeOffset.Now.AddDays(30)
                        };

                        Response.Cookies.Append("jwt_token", result.AccessToken, cookieOptions);
                        Response.Cookies.Append("refresh_token", result.RefreshToken, cookieOptions);
                    }

                    return Json(new
                    {
                        success = true,
                        token = result.AccessToken,
                        refreshToken = result.RefreshToken
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token refresh error");
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = false, message = "Token refresh failed" });
            }

            return RedirectToAction("Login");
        }


        [HttpGet]
        public IActionResult GetCurrentToken()
        {
            try
            {
                var token = HttpContext.Session.GetString("JwtToken");
                if (string.IsNullOrEmpty(token))
                {
                    return Json(new { success = false, message = "No token available" });
                }

                return Json(new { success = true, token = token });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error retrieving token" });
            }
        }
    }
}