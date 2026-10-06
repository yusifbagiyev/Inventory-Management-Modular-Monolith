using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedServices.Exceptions;

namespace SharedServices.Web
{
    /// <summary>Turns /api exceptions into JSON errors without ever leaking an unexpected error's message.</summary>
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
            // The file system throws this type too, for a folder it cannot write, and that is not a sign-in problem
            UnauthorizedAccessException when UserFacingErrors.IsUserFacing(exception)
                => (StatusCodes.Status401Unauthorized, new { error = exception.Message }),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict,
                new { error = "The record has been changed by another user. Please reload the page and try again." }),
            // Domain rule violations, while the same types from the framework or driver are faults and fall through
            ArgumentException or InvalidOperationException when UserFacingErrors.IsUserFacing(exception)
                => (StatusCodes.Status400BadRequest, new { error = exception.Message }),
            _ => (StatusCodes.Status500InternalServerError, new { error = UserFacingErrors.Generic })
        };
    }
}
