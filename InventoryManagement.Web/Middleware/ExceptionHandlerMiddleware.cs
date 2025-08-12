using System.Net;
using System.Text.Json;

namespace InventoryManagement.Web.Middleware
{
    public class ExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlerMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;
        public ExceptionHandlerMiddleware(
            RequestDelegate next,
            ILogger<ExceptionHandlerMiddleware> logger,
            IWebHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context,ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            // Enhanced structured logging for better Seq integration
            var userId = context.User?.Identity?.Name ?? "Anonymous";
            var requestPath = context.Request.Path.Value ?? "Unknown";
            var requestMethod = context.Request.Method;
            var userAgent = context.Request.Headers["User-Agent"].FirstOrDefault() ?? "Unknown";
            var requestId = context.TraceIdentifier;

            // Log the exception with structured data that Seq can easily query
            _logger.LogError(exception,
                "Unhandled exception occurred for {UserId} on {RequestPath} ({RequestMethod}). " +
                "Request ID: {RequestId}, User Agent: {UserAgent}",
                userId, requestPath, requestMethod, requestId, userAgent);

            var response = context.Response;
            response.ContentType = "application/json";

            var errorResponse = new ErrorResponse
            {
                TraceId = context.TraceIdentifier,
                Timestamp = DateTime.UtcNow, // Use UTC for consistency
                RequestPath = requestPath,
                RequestMethod = requestMethod
            };

            switch (exception)
            {
                case UnauthorizedAccessException:
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    errorResponse.Message = "You are not authorized to access this resource";
                    errorResponse.Type = "UnauthorizedAccess";

                    // Log security-related events with additional context
                    _logger.LogWarning("Unauthorized access attempt by {UserId} to {RequestPath}",
                        userId, requestPath);
                    break;

                case KeyNotFoundException:
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    errorResponse.Message = "The requested resource was not found";
                    errorResponse.Type = "NotFound";

                    _logger.LogInformation("Resource not found: {RequestPath} for user {UserId}",
                        requestPath, userId);
                    break;

                case InvalidOperationException:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    errorResponse.Message = exception.Message;
                    errorResponse.Type = "InvalidOperation";

                    _logger.LogWarning("Invalid operation attempted: {ExceptionMessage} by {UserId} on {RequestPath}",
                        exception.Message, userId, requestPath);
                    break;

                case HttpRequestException:
                    response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                    errorResponse.Message = "Unable to connect to the service. Please try again later.";
                    errorResponse.Type = "ServiceUnavailable";

                    _logger.LogError("Service unavailable error: {ExceptionMessage} for request {RequestPath}",
                        exception.Message, requestPath);
                    break;

                case TaskCanceledException:
                    response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                    errorResponse.Message = "The request timed out. Please try again.";
                    errorResponse.Type = "RequestTimeout";

                    _logger.LogWarning("Request timeout for {RequestPath} by {UserId}",
                        requestPath, userId);
                    break;

                case ArgumentException:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    errorResponse.Message = _environment.IsDevelopment()
                        ? exception.Message
                        : "Invalid request parameters";
                    errorResponse.Type = "BadRequest";

                    _logger.LogWarning("Bad request with argument exception: {ExceptionMessage} for {RequestPath}",
                        exception.Message, requestPath);
                    break;

                default:
                    response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    errorResponse.Message = _environment.IsDevelopment()
                        ? exception.Message
                        : "An error occurred while processing your request";
                    errorResponse.Type = "InternalServerError";

                    // Log critical errors with full context
                    _logger.LogError("Critical error of type {ExceptionType}: {ExceptionMessage} " +
                                   "for user {UserId} on {RequestPath}. Stack trace: {StackTrace}",
                        exception.GetType().Name, exception.Message, userId, requestPath, exception.StackTrace);
                    break;
            }

            // Include development details only in development environment
            if (_environment.IsDevelopment())
            {
                errorResponse.Details = exception.StackTrace;
                errorResponse.InnerException = exception.InnerException?.Message;
            }

            // Handle AJAX requests differently
            if (IsAjaxRequest(context.Request))
            {
                var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                await response.WriteAsync(jsonResponse);
            }
            else
            {
                // For non-AJAX requests, redirect to error page
                context.Items["ErrorResponse"] = errorResponse;
                context.Response.Redirect($"/Home/Error?statusCode={response.StatusCode}");
            }

            // Log the completion of error handling
            _logger.LogInformation("Error response sent for request {RequestId} with status code {StatusCode}",
                requestId, response.StatusCode);
        }
        private bool IsAjaxRequest(HttpRequest request)
        {
            return request.Headers["X-Requested-Width"] == "XMLHttpRequest" ||
                   request.ContentType?.Contains("application/json") == true ||
                   request.Headers.Accept.ToString().Contains("application/json");
        }
    }
    public record ErrorResponse
    {
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Details { get; set; }
        public string? InnerException { get; set; }
        public string TraceId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string RequestPath { get; set; } = string.Empty;
        public string RequestMethod { get; set; } = string.Empty;
    }
}