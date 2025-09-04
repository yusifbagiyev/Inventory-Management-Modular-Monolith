using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
    public abstract class BaseController : Controller
    {
        protected readonly ILogger<BaseController>? _logger;
        private readonly IServiceProvider? _serviceProvider;

        protected BaseController(ILogger<BaseController>? logger = null, IServiceProvider? serviceProvider = null)
        {
            _logger = logger;
            _serviceProvider = serviceProvider ?? HttpContext?.RequestServices;
        }


        /// <summary>
        /// Gets the current JWT token, refreshing it if necessary
        /// </summary>
        protected async Task<string?> GetValidJwtTokenAsync()
        {
            var token = GetStoredToken();

            if (string.IsNullOrEmpty(token))
                return null;

            // Check if token needs refresh
            if (ShouldRefreshToken(token))
            {
                var refreshed = await RefreshTokenAsync();
                return refreshed ? GetStoredToken() : null;
            }

            return token;
        }

        private string? GetStoredToken()
        {
            // Try session first, then cookies
            return HttpContext.Session.GetString("JwtToken")
                ?? Request.Cookies["jwt_token"];
        }

        private bool ShouldRefreshToken(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                if (!handler.CanReadToken(token))
                    return true;

                var jwtToken = handler.ReadJwtToken(token);
                var timeUntilExpiry = jwtToken.ValidTo - DateTime.UtcNow;

                return timeUntilExpiry.TotalMinutes < 5; // Refresh if less than 5 minutes remaining
            }
            catch
            {
                return true;
            }
        }
        private async Task<bool> RefreshTokenAsync()
        {
            try
            {
                var authService = _serviceProvider?.GetService<IAuthService>();
                if (authService == null)
                    return false;

                var currentToken = GetStoredToken();
                var refreshToken = HttpContext.Session.GetString("RefreshToken")
                    ?? Request.Cookies["refresh_token"];

                if (string.IsNullOrEmpty(currentToken) || string.IsNullOrEmpty(refreshToken))
                    return false;

                var result = await authService.RefreshTokenAsync(currentToken, refreshToken);

                if (result != null && !string.IsNullOrEmpty(result.AccessToken))
                {
                    // Update session
                    HttpContext.Session.SetString("JwtToken", result.AccessToken);
                    HttpContext.Session.SetString("RefreshToken", result.RefreshToken);
                    HttpContext.Session.SetString("UserData", JsonConvert.SerializeObject(result.User));

                    // Update cookies if they existed
                    if (Request.Cookies.ContainsKey("jwt_token"))
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

                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error refreshing token");
            }

            return false;
        }

        /// <summary>
        /// Checks if the current request is an AJAX request
        /// </summary>
        protected bool IsAjaxRequest()
        {
            return Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        }



        /// <summary>
        /// Handles API responses uniformly for both AJAX and traditional requests
        /// </summary>
        protected IActionResult HandleApiResponse<T>(ApiResponse<T> response, string redirectAction)
        {
            if (response.IsSuccess)
            {
                // Only set TempData for non-AJAX requests
                if (!IsAjaxRequest())
                {
                    TempData["Success"] = response.Message ?? "Operation completed successfully";
                }
                return RedirectToAction(redirectAction);
            }

            if (response.IsApprovalRequest)
            {
                if (!IsAjaxRequest())
                {
                    TempData["Info"] = response.Message ?? "Request submitted for approval";
                }
                return RedirectToAction(redirectAction);
            }

            if (!IsAjaxRequest())
            {
                TempData["Error"] = response.Message ?? "Operation failed";
            }
            return RedirectToAction(redirectAction);
        }

        /// <summary>
        /// Handles errors uniformly
        /// </summary>
        protected IActionResult HandleError(string errorMessage, object? model = null,
            Dictionary<string, string>? fieldErrors = null)
        {
            _logger?.LogError("Error in {Controller}: {ErrorMessage}",
                ControllerContext.ActionDescriptor.ControllerName, errorMessage);

            if (IsAjaxRequest())
            {
                var response = new
                {
                    isSuccess = false,
                    message = errorMessage,
                    errors = fieldErrors
                };

                Response.StatusCode = 400; // Bad Request
                return Json(response);
            }

            ModelState.AddModelError("", errorMessage);

            if (fieldErrors != null)
            {
                foreach (var error in fieldErrors)
                {
                    ModelState.AddModelError(error.Key, error.Value);
                }
            }

            return View(model);
        }



        /// <summary>
        /// Handles exceptions uniformly
        /// </summary>
        protected IActionResult HandleException(Exception ex, object? model = null)
        {
            _logger?.LogError(ex, "Exception in {Controller}.{Action}",
                ControllerContext.ActionDescriptor.ControllerName,
                ControllerContext.ActionDescriptor.ActionName);

            string userFriendlyMessage = "An unexpected error occurred. Please try again.";

            // Provide more specific messages for common exceptions
            if (ex is UnauthorizedAccessException)
            {
                userFriendlyMessage = "You don't have permission to perform this action.";
                if (IsAjaxRequest())
                {
                    Response.StatusCode = 403;
                }
            }
            else if (ex is InvalidOperationException && ex.Message.Contains("inventory code"))
            {
                userFriendlyMessage = ex.Message;
            }
            else if (ex is HttpRequestException)
            {
                userFriendlyMessage = "Unable to connect to the server. Please check your connection.";
            }

            return HandleError(userFriendlyMessage, model);
        }

        /// <summary>
        /// Helper to get current user ID
        /// </summary>
        protected int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        /// <summary>
        /// Helper to get current username
        /// </summary>
        protected string GetCurrentUserName()
        {
            return User.Identity?.Name ?? "Unknown";
        }

        /// <summary>
        /// Handles validation errors from ModelState
        /// </summary>
        protected IActionResult HandleValidationErrors(object? model = null)
        {
            if (IsAjaxRequest())
            {
                var errors = ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray()
                    );

                return Json(new
                {
                    isSuccess = false,
                    message = "Please correct the validation errors and try again.",
                    errors
                });
            }

            return View(model);
        }



        /// <summary>
        /// Parses error message from API response
        /// </summary>
        protected string ParseApiErrorMessage(string responseContent, string defaultMessage = "Operation failed")
        {
            if (string.IsNullOrWhiteSpace(responseContent))
                return defaultMessage;

            try
            {
                // Try to parse as JSON
                using var doc = JsonDocument.Parse(responseContent);
                var root = doc.RootElement;

                // Check various common error format
                if (root.TryGetProperty("error", out var errorProp))
                    return errorProp.GetString() ?? defaultMessage;

                if (root.TryGetProperty("message", out var messageProp))
                    return messageProp.GetString() ?? defaultMessage;

                if (root.TryGetProperty("title", out var titleProp))
                    return titleProp.GetString() ?? defaultMessage;

                if (root.TryGetProperty("errors", out var errorsProp) && errorsProp.ValueKind == JsonValueKind.Object)
                {
                    var errorMessages = new List<string?>();
                    foreach (var error in errorsProp.EnumerateObject())
                    {
                        if (error.Value.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var msg in error.Value.EnumerateArray())
                            {
                                errorMessages.Add(msg.GetString());
                            }
                        }
                        else
                        {
                            errorMessages.Add(error.Value.GetString());
                        }
                    }
                    return string.Join("; ", errorMessages);
                }
            }
            catch
            {
                // If not JSON or parsing fails, return the content if it's short enough
                if (responseContent.Length < 200 && !responseContent.Contains("<"))
                {
                    return responseContent;
                }
            }

            return defaultMessage;
        }



        /// <summary>
        /// Creates a standardized JSON response for AJAX requests
        /// </summary>
        protected IActionResult AjaxResponse(bool success, string message, object? data = null,
            Dictionary<string, string[]>? errors = null)
        {
            return Json(new
            {
                isSuccess = success,
                message,
                data,
                errors
            });
        }
    }
}