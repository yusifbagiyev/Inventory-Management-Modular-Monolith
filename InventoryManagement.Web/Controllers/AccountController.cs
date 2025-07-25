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
        private readonly ILogger<AccountController> _logger;
        public AccountController(
            IAuthService authService,
            ILogger<AccountController> logger)
        {
            _authService = authService;
            _logger= logger;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated ?? false)
            {
                //Check if we have a valid JWT tokens
                var token = HttpContext.Session.GetString("JwtToken");
                if (string.IsNullOrEmpty(token))
                {
                    // User is authenticated via cookie but has no JWT token
                    // Sign them out and continue with login
                    HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).Wait();
                    HttpContext.Session.Clear();
                }
                else
                {
                    // User has valid session, redirect to home
                    return RedirectToAction("Index", "Home");
                }
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
                    var result = await _authService.LoginAsync(model.Username, model.Password);
                    if (result != null && !string.IsNullOrEmpty(result.AccessToken))
                    {
                        // Store JWT tokens
                        if (model.RememberMe)
                        {
                            // Use cookies for persistent storage when Remember Me is checked
                            var cookieOptions = new CookieOptions
                            {
                                HttpOnly = true,
                                Secure = true,
                                SameSite = SameSiteMode.Strict,
                                Expires = DateTimeOffset.UtcNow.AddDays(30)
                            };

                            Response.Cookies.Append("jwt_token", result.AccessToken, cookieOptions);
                            Response.Cookies.Append("refresh_token", result.RefreshToken, cookieOptions);
                            Response.Cookies.Append("user_data", JsonConvert.SerializeObject(result.User), cookieOptions);
                        }
                        else
                        {
                            // Use session storage for non-persistent login
                            HttpContext.Session.SetString("JwtToken", result.AccessToken);
                            HttpContext.Session.SetString("RefreshToken", result.RefreshToken);
                            HttpContext.Session.SetString("UserData", JsonConvert.SerializeObject(result.User));
                        }

                        // Create claims for cookie authentication
                        var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, result.User.Id.ToString()),
                    new Claim(ClaimTypes.Name, result.User.Username),
                    new Claim(ClaimTypes.Email, result.User.Email),
                    new Claim("FirstName", result.User.FirstName),
                    new Claim("LastName", result.User.LastName),
                    new Claim("RememberMe", model.RememberMe.ToString())
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
                            IsPersistent = model.RememberMe,
                            ExpiresUtc = model.RememberMe ?
                                DateTimeOffset.UtcNow.AddDays(30) :
                                DateTimeOffset.UtcNow.AddHours(8)
                        };

                        await HttpContext.SignInAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            new ClaimsPrincipal(claimsIdentity),
                            authProperties);

                        _logger.LogInformation($"User {model.Username} logged in successfully with RememberMe={model.RememberMe}");

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
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();

            _logger.LogInformation("User logged out");

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
            var accessToken = HttpContext.Session.GetString("JwtToken");
            var refreshToken = HttpContext.Session.GetString("RefreshToken");

            if(string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
            {
                return RedirectToAction("Login");
            }

            try
            {
                var result = await _authService.RefreshTokenAsync(accessToken, refreshToken);

                if (result != null)
                {
                    HttpContext.Session.SetString("JwtToken", result.AccessToken);
                    HttpContext.Session.SetString("RefreshToken", result.RefreshToken);

                    return Ok(new { success = true });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token refresh error");
            }

            return RedirectToAction("Login");
        }

    }
}