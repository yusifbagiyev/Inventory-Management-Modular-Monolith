using System.Text.Json;
using InventoryManagement.Web.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
    public abstract class BaseController : Controller
    {
        protected readonly ILogger<BaseController>? _logger;

        protected BaseController(ILogger<BaseController>? logger = null)
        {
            _logger = logger;
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
            if (IsAjaxRequest())
            {
                // Return JSON with proper status code
                if (!response.IsSuccess && !response.IsApprovalRequest)
                {
                    Response.StatusCode = 400; // Set proper error status
                }
                // For AJAX requests, return JSON
                return Json(new
                {
                    isSuccess = response.IsSuccess,
                    isApprovalRequest = response.IsApprovalRequest,
                    approvalRequestId = response.ApprovalRequestId,
                    message = response.Message,
                    data = response.Data
                });
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
        /// Handles validation errors from ModelState
        /// </summary>
        protected IActionResult HandleValidationErrors(object? model = null)
        {
            if (IsAjaxRequest())
            {
                Response.StatusCode = 400; // Important: Set error status code

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
    }
}