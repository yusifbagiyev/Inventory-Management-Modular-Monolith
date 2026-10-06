using System.Net;
using System.Text.Json;

namespace InventoryManagement.Web.Middleware
{
    /// <summary>Last-resort handler that logs an unhandled exception and answers with JSON or the error page.</summary>
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
            var userId = context.User?.Identity?.Name ?? "Anonymous";
            var requestPath = context.Request.Path.Value ?? "Unknown";
            var requestMethod = context.Request.Method;
            var requestId = context.TraceIdentifier;

            var response = context.Response;
            response.ContentType = "application/json";

            var errorResponse = new ErrorResponse
            {
                TraceId = context.TraceIdentifier,
                Timestamp = DateTime.Now,
                RequestPath = requestPath,
                RequestMethod = requestMethod
            };

            // Framework, EF Core and LINQ throw these same types for real bugs, so only the modules' own ones count as the caller's mistake
            var callerMistake = SharedServices.Web.UserFacingErrors.IsUserFacing(exception);

            switch (exception)
            {
                case UnauthorizedAccessException:
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    errorResponse.Message = "You are not authorized to access this resource";
                    errorResponse.Type = "UnauthorizedAccess";
                    break;

                case KeyNotFoundException:
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    errorResponse.Message = "The requested resource was not found";
                    errorResponse.Type = "NotFound";
                    break;

                case InvalidOperationException:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    // Internal text stays out of production responses, the full message is still logged
                    errorResponse.Message = _environment.IsDevelopment()
                        ? exception.Message
                        : "The request could not be completed";
                    errorResponse.Type = "InvalidOperation";
                    break;

                case HttpRequestException:
                    response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                    errorResponse.Message = "Unable to connect to the service. Please try again later.";
                    errorResponse.Type = "ServiceUnavailable";
                    callerMistake = false;
                    break;

                case TaskCanceledException:
                    response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                    errorResponse.Message = "The request timed out. Please try again.";
                    errorResponse.Type = "RequestTimeout";
                    // A browser that went away is no failure, while any other timeout is
                    callerMistake = context.RequestAborted.IsCancellationRequested;
                    break;

                case ArgumentException:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    errorResponse.Message = _environment.IsDevelopment()
                        ? exception.Message
                        : "Invalid request parameters";
                    errorResponse.Type = "BadRequest";
                    break;

                default:
                    response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    errorResponse.Message = _environment.IsDevelopment()
                        ? exception.Message
                        : "An error occurred while processing your request";
                    errorResponse.Type = "InternalServerError";
                    callerMistake = false;
                    break;
            }

            if (callerMistake)
                _logger.LogWarning(exception, "{ErrorType} on {RequestMethod} {RequestPath} for {UserId}: {ExceptionMessage} ({RequestId})",
                    errorResponse.Type, requestMethod, requestPath, userId, exception.Message, requestId);
            else
                _logger.LogError(exception, "Unhandled {ExceptionType} on {RequestMethod} {RequestPath} for {UserId} ({RequestId})",
                    exception.GetType().Name, requestMethod, requestPath, userId, requestId);

            if (_environment.IsDevelopment())
            {
                errorResponse.Details = exception.StackTrace;
                errorResponse.InnerException = exception.InnerException?.Message;
            }

            if (IsAjaxRequest(context.Request))
            {
                var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                await response.WriteAsync(jsonResponse);
            }
            else if (IsErrorPage(context.Request))
            {
                // The error page itself failed, for example while the sign-in cookie was checked, so redirecting again would loop
                response.ContentType = "text/html; charset=utf-8";
                await response.WriteAsync("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Error</title></head>"
                    + "<body style=\"font-family: sans-serif; padding: 2rem\"><h1>An error has occurred</h1>"
                    + $"<p>Please try again in a moment. Reference: {WebUtility.HtmlEncode(requestId)}</p></body></html>");
            }
            else
            {
                // The reference is this request's, so the error page can show what the log was written under
                context.Response.Redirect($"/Home/Error?statusCode={response.StatusCode}&ref={Uri.EscapeDataString(requestId)}");
            }
        }
        private static bool IsErrorPage(HttpRequest request)
            => request.Path.StartsWithSegments("/Home/Error", StringComparison.OrdinalIgnoreCase)
               || request.Path.StartsWithSegments("/NotFound", StringComparison.OrdinalIgnoreCase);

        private bool IsAjaxRequest(HttpRequest request)
        {
            return request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
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