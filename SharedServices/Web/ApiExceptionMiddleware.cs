using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedServices.Exceptions;

namespace SharedServices.Web
{
    /// <summary>
    /// Maps exceptions thrown by /api endpoints to JSON error responses
    /// (<c>{ error, validationErrors? }</c>). Unexpected errors never leak their message.
    /// </summary>
    public sealed class ApiExceptionMiddleware
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private readonly RequestDelegate _next;
        private readonly ILogger<ApiExceptionMiddleware> _logger;

        public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex) when (!context.Response.HasStarted)
            {
                var (status, body) = Map(ex);
                if (status >= 500)
                    _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
                else
                    _logger.LogInformation("Request {Method} {Path} failed with {Status}: {Message}",
                        context.Request.Method, context.Request.Path, status, ex.Message);

                context.Response.Clear();
                context.Response.StatusCode = status;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
            }
        }

        private static (int Status, object Body) Map(Exception exception) => exception switch
        {
            ValidationException validation => (StatusCodes.Status400BadRequest, new
            {
                error = "Validation Error",
                validationErrors = validation.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            }),
            ApprovalRequiredException approval => (StatusCodes.Status202Accepted, new
            {
                approvalRequestId = approval.ApprovalRequestId,
                message = approval.Message,
                status = approval.Status
            }),
            NotFoundException => (StatusCodes.Status404NotFound, new { error = exception.Message }),
            ConflictException or DuplicateEntityException => (StatusCodes.Status409Conflict, new { error = exception.Message }),
            InsufficientPermissionsException => (StatusCodes.Status403Forbidden, new { error = exception.Message }),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, new { error = exception.Message }),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict,
                new { error = "The record was changed by someone else. Reload and try again." }),
            // Domain rule violations ("route is already completed", "request is not pending"); the
            // same types from the framework or the database driver keep their text in the log.
            ArgumentException or InvalidOperationException
                => (StatusCodes.Status400BadRequest, new { error = UserFacingErrors.MessageOf(exception) }),
            _ => (StatusCodes.Status500InternalServerError, new { error = "An error occurred while processing your request" })
        };
    }
}
