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
                // Check if we have a valid token
                var validToken = await _tokenManager.GetValidTokenAsync();
                if (!string.IsNullOrEmpty(validToken))
                {
                    return RedirectToAction("Index", "Home");
                }

                // If token is invalid, clean up and show login
                await CleanupAuthenticationAsync();
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                try
                {
                    var result = await _authService.LoginAsync(model.Username, model.Password, model.RememberMe);
                    if (result != null && !string.IsNullOrEmpty(result.AccessToken))
                    {
                        // SECURITY: Only store access token in session (server-side memory)
                        HttpContext.Session.SetString("JwtToken", result.AccessToken);

                        // Store minimal user info in session
                        HttpContext.Session.SetString("UserData", JsonConvert.SerializeObject(new
                        {
                            result.User.Id,
                            result.User.Username,
                            result.User.Email,
                            result.User.FirstName,
                            result.User.LastName
                        }));

                        // Get the actual RememberMe value (use our parameter if result doesn't have it)
                        var rememberMe = result.RememberMe ?? model.RememberMe;

                        // SECURITY: Store refresh token in HttpOnly cookie
                        var refreshCookieOptions = new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = Request.IsHttps,
                            SameSite = SameSiteMode.Strict,
                            Expires = rememberMe
                                ? DateTimeOffset.Now.AddDays(30)
                                : DateTimeOffset.Now.AddHours(1),
                            Path = "/",
                            IsEssential = true
                        };
                        Response.Cookies.Append("refresh_token", result.RefreshToken, refreshCookieOptions);

                        // Store Remember Me preference
                        if (rememberMe)
                        {
                            var rememberCookieOptions = new CookieOptions
                            {
                                HttpOnly = false,
                                Secure = Request.IsHttps,
                                SameSite = SameSiteMode.Strict,
                                Expires = DateTimeOffset.Now.AddDays(365),
                                Path = "/"
                            };
                            Response.Cookies.Append("remember_me", "true", rememberCookieOptions);
                            Response.Cookies.Append("username", model.Username, rememberCookieOptions);
                        }
                        else
                        {
                            // Clear remember me cookies if user didn't check the box
                            Response.Cookies.Delete("remember_me");
                            Response.Cookies.Delete("username");
                        }

                        // Store last activity time
                        HttpContext.Session.SetString("LastActivity", DateTime.Now.ToString("o"));

                        // Create authentication claims
                        var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.NameIdentifier, result.User.Id.ToString()),
                            new Claim(ClaimTypes.Name, result.User.Username),
                            new Claim(ClaimTypes.Email, result.User.Email),
                            new Claim("FirstName", result.User.FirstName),
                            new Claim("LastName", result.User.LastName),
                            new Claim("RememberMe", rememberMe.ToString()),
                            new Claim("LoginTime", DateTime.Now.ToString("o"))
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

                        var authProperties = new AuthenticationProperties
                        {
                            IsPersistent = rememberMe,
                            ExpiresUtc = rememberMe
                                ? DateTimeOffset.Now.AddDays(30)
                                : DateTimeOffset.Now.AddHours(8),
                            AllowRefresh = true,
                            IssuedUtc = DateTimeOffset.Now
                        };

                        await HttpContext.SignInAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            new ClaimsPrincipal(claimsIdentity),
                            authProperties);

                        _logger.LogInformation(
                            "User {Username} logged in successfully with RememberMe={RememberMe}",
                            model.Username,
                            rememberMe);

                        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        {
                            return Redirect(returnUrl);
                        }

                        return RedirectToAction("Dashboard", "Home");
                    }

                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Login error");
                    ModelState.AddModelError(string.Empty, "An error occurred during login. Please try again.");
                }
            }

            return View(model);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            _logger.LogInformation($"User - {User?.Identity?.Name} logged out");
            await CleanupAuthenticationAsync();
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
            // Get refresh token from HttpOnly cookie
            var refreshToken = Request.Cookies["refresh_token"];
            var accessToken = HttpContext.Session.GetString("jwt_token");

            if (string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(accessToken))
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "No refresh token available" });
                }
                return RedirectToAction("Login");
            }

            try
            {
                var result = await _authService.RefreshTokenAsync(refreshToken,accessToken);

                if (result != null)
                {
                    // Update session with new access token
                    HttpContext.Session.SetString("JwtToken", result.AccessToken);
                    HttpContext.Session.SetString("UserData", JsonConvert.SerializeObject(new
                    {
                        result.User.Id,
                        result.User.Username,
                        result.User.Email,
                        result.User.FirstName,
                        result.User.LastName
                    }));
                    HttpContext.Session.SetString("LastActivity", DateTime.Now.ToString("o"));

                    // Update refresh token cookie with new token (token rotation)
                    var rememberMe = Request.Cookies["remember_me"] == "true";
                    var refreshCookieOptions = new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = Request.IsHttps,
                        SameSite = SameSiteMode.Strict,
                        Expires = rememberMe
                            ? DateTimeOffset.Now.AddDays(30)
                            : DateTimeOffset.Now.AddHours(1),
                        Path = "/",
                        IsEssential = true
                    };
                    Response.Cookies.Append("refresh_token", result.RefreshToken, refreshCookieOptions);

                    return Json(new
                    {
                        success = true
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


        private async Task CleanupAuthenticationAsync()
        {
            // Sign out from cookie authentication
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Clear session
            HttpContext.Session.Clear();

            // Clear the refresh token cookie (but keep remember_me and username if they exist)
            Response.Cookies.Delete("refresh_token", new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = "/"
            });

            Response.Cookies.Delete("remember_me");
            Response.Cookies.Delete("username");
        }
    }
}