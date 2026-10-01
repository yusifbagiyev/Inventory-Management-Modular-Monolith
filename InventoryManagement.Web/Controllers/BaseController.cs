using System.Security.Claims;
using FluentValidation;
using InventoryManagement.Web.Localization;
using InventoryManagement.Web.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedServices.Exceptions;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
    public abstract class BaseController : Controller
    {
        protected readonly ILogger? _logger;

        protected BaseController(ILogger? logger = null)
        {
            _logger = logger;
        }

        protected bool IsAjaxRequest() => Request.Headers.XRequestedWith == "XMLHttpRequest";

        protected int GetCurrentUserId()
            => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        protected string GetCurrentUserName() => User.Identity?.Name ?? "Unknown";

        /// <summary>The "permission" claims of the signed-in user.</summary>
        protected List<string> GetCurrentUserPermissions()
            => User.Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToList();

        protected IActionResult RedirectToNotFound() => RedirectToAction("NotFound", "Home", new { statusCode = 404 });

        /// <summary>
        /// Runs a module call and turns the expected outcomes into an <see cref="ApiResponse{T}"/>:
        /// success, "submitted for approval", or a user-facing error message. Unexpected exceptions
        /// propagate to the caller's catch / the exception middleware.
        /// </summary>
        protected async Task<ApiResponse<T>> RunAsync<T>(Func<Task<T>> action, string? successMessage = null)
        {
            try
            {
                return new ApiResponse<T> { IsSuccess = true, Data = await action(), Message = Tr(successMessage) };
            }
            catch (ApprovalRequiredException ex)
            {
                return new ApiResponse<T>
                {
                    IsSuccess = false,
                    IsApprovalRequest = true,
                    ApprovalRequestId = ex.ApprovalRequestId,
                    Message = JsonStringLocalizer.TranslateMessage(ex.Message)
                };
            }
            catch (ValidationException ex)
            {
                return Failure<T>(string.Join("; ", ex.Errors.Select(e => Tr(e.ErrorMessage)).Distinct()));
            }
            catch (DbUpdateConcurrencyException)
            {
                return Failure<T>(Tr("The record was changed by someone else. Reload and try again."));
            }
            catch (Exception ex) when (ex is NotFoundException or DuplicateEntityException or ConflictException
                                          or InsufficientPermissionsException or InvalidOperationException
                                          or ArgumentException or UnauthorizedAccessException)
            {
                // Framework and database exceptions of these types carry internal text: logged, not shown.
                if (!SharedServices.Web.UserFacingErrors.IsUserFacing(ex))
                    HttpContext.RequestServices.GetRequiredService<ILogger<BaseController>>()
                        .LogWarning(ex, "Request failed: {Message}", ex.Message);
                return Failure<T>(Tr(SharedServices.Web.UserFacingErrors.MessageOf(ex)));
            }
        }

        protected Task<ApiResponse<bool>> RunAsync(Func<Task> action, string? successMessage = null)
            => RunAsync(async () => { await action(); return true; }, successMessage);

        private static ApiResponse<T> Failure<T>(string message) => new() { IsSuccess = false, Message = message };

        /// <summary>Translates a user-facing message into the interface language (see Resources/i18n/az.json).</summary>
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(message))]
        protected static string? Tr(string? message)
            => message is null ? null : JsonStringLocalizer.TranslateMessage(message);

        /// <summary>
        /// Handles a module response uniformly: JSON for AJAX callers; otherwise a redirect, with
        /// the outcome surfaced through the layout's TempData toast.
        /// </summary>
        protected IActionResult HandleApiResponse<T>(ApiResponse<T> response, string redirectAction)
        {
            if (IsAjaxRequest())
            {
                if (!response.IsSuccess && !response.IsApprovalRequest)
                    Response.StatusCode = StatusCodes.Status400BadRequest;

                return Json(new
                {
                    isSuccess = response.IsSuccess,
                    isApprovalRequest = response.IsApprovalRequest,
                    approvalRequestId = response.ApprovalRequestId,
                    message = response.Message,
                    data = response.Data
                });
            }

            if (response.IsSuccess || response.IsApprovalRequest)
            {
                if (!string.IsNullOrEmpty(response.Message))
                    TempData["Success"] = response.Message;
            }
            else
            {
                TempData["Error"] = response.Message ?? Tr("Operation failed");
            }

            return RedirectToAction(redirectAction);
        }

        protected IActionResult HandleError(string errorMessage, object? model = null,
            Dictionary<string, string>? fieldErrors = null)
        {
            errorMessage = Tr(errorMessage);
            _logger?.LogWarning("Error in {Controller}: {ErrorMessage}",
                ControllerContext.ActionDescriptor.ControllerName, errorMessage);

            if (IsAjaxRequest())
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return Json(new { isSuccess = false, message = errorMessage, errors = fieldErrors });
            }

            ModelState.AddModelError("", errorMessage);
            if (fieldErrors != null)
            {
                foreach (var error in fieldErrors)
                    ModelState.AddModelError(error.Key, Tr(error.Value));
            }
            return View(model);
        }

        protected IActionResult HandleException(Exception ex, object? model = null)
        {
            _logger?.LogError(ex, "Exception in {Controller}.{Action}",
                ControllerContext.ActionDescriptor.ControllerName,
                ControllerContext.ActionDescriptor.ActionName);

            if (ex is UnauthorizedAccessException or InsufficientPermissionsException)
            {
                if (IsAjaxRequest())
                    Response.StatusCode = StatusCodes.Status403Forbidden;
                return HandleError("You don't have permission to perform this action.", model);
            }

            return HandleError("An unexpected error occurred. Please try again.", model);
        }

        protected IActionResult HandleValidationErrors(object? model = null)
        {
            if (IsAjaxRequest())
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                var errors = ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value?.Errors.Select(e => Tr(e.ErrorMessage)).ToArray());

                return Json(new
                {
                    isSuccess = false,
                    message = Tr("Please correct the validation errors and try again."),
                    errors
                });
            }
            return View(model);
        }

        protected IActionResult AjaxResponse(bool success, string message, object? data = null,
            Dictionary<string, string[]>? errors = null)
            => Json(new { isSuccess = success, message = Tr(message), data, errors });
    }
}
