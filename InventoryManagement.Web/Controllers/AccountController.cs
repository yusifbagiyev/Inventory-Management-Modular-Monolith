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
            // Check if user already has a valid JWT token in session
            var token = HttpContext.Session.GetString("JwtToken");
            if (!string.IsNullOrEmpty(token) && IsTokenValid(token))
            {
                return RedirectToAction("Dashboard", "Home");
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
                return View(model);

            try
            {
                var result = await _authService.LoginAsync(model.Username, model.Password);

                if (result != null && !string.IsNullOrEmpty(result.AccessToken))
                {
                    // Store JWT tokens in session
                    HttpContext.Session.SetString("JwtToken", result.AccessToken);
                    HttpContext.Session.SetString("RefreshToken", result.RefreshToken);
                    HttpContext.Session.SetString("UserData", JsonConvert.SerializeObject(result.User));

                    // If "Remember Me" is checked, also store in persistent cookies
                    if (model.RememberMe)
                    {
                        var cookieOptions = new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = HttpContext.Request.IsHttps,
                            SameSite = SameSiteMode.Lax,
                            Expires = DateTimeOffset.Now.AddDays(30)
                        };

                        Response.Cookies.Append("jwt_token", result.AccessToken, cookieOptions);
                        Response.Cookies.Append("refresh_token", result.RefreshToken, cookieOptions);
                    }

                    _logger.LogInformation($"User {model.Username} logged in successfully");

                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return Redirect(returnUrl);

                    return RedirectToAction("Dashboard", "Home");
                }

                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error for user {Username}", model.Username);
                ModelState.AddModelError(string.Empty, "An error occurred during login. Please try again.");
            }

            return View(model);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();

            // Clear the JWT cookies
            if (Request.Cookies.ContainsKey("jwt_token"))
            {
                Response.Cookies.Delete("jwt_token");
                Response.Cookies.Delete("refresh_token");
            }
            _logger.LogInformation("User logged out");
            return RedirectToAction("Login");
        }



        public IActionResult AccessDenied()
        {
            return View();
        }


        [HttpGet]
        public IActionResult Profile()
        {
            var userDataJson = HttpContext.Session.GetString("UserData");
            if (string.IsNullOrEmpty(userDataJson))
            {
                return RedirectToAction("Login");
            }

            var userData = JsonConvert.DeserializeObject<User>(userDataJson);
            return View(userData);
        }

        private bool IsTokenValid(string token)
        {
            try
            {
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                if (!handler.CanReadToken(token))
                    return false;

                var jwtToken = handler.ReadJwtToken(token);
                return jwtToken.ValidTo > DateTime.UtcNow.AddMinutes(5); // Valid for at least 5 more minutes
            }
            catch
            {
                return false;
            }
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