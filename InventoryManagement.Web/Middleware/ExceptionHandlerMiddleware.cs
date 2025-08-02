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

        private async Task HandleExceptionAsync(HttpContext context,Exception exception)
        {
            _logger.LogError(exception, "An unhandled exception occured");
            
            var response=context.Response;
            response.ContentType = "application/json";

            var errorResponse = new ErrorResponse
            {
                TraceId = context.TraceIdentifier,
                Timestamp= DateTime.UtcNow
            };

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
                    errorResponse.Message = exception.Message;
                    errorResponse.Type = "InvalidOperation";
                    break;

                case HttpRequestException:
                    response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                    errorResponse.Message = "Unable to connect to the service. Please try again later.";
                    errorResponse.Type = "ServiceUnavailable";
                    break;

                default:
                    response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    errorResponse.Message = _environment.IsDevelopment()
                        ? exception.Message
                        : "An error occurred while processing your request";
                    errorResponse.Type = "InternalServerError";
                    break;
            }

            if(_environment.IsDevelopment())
            {
                errorResponse.Details = exception.StackTrace;
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
                // For non-AJAX requests,redirect to error page
                context.Items["ErrorResponse"] = errorResponse;
                context.Response.Redirect($"/Home/Error?statusCode={response.StatusCode}");
            }
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
        public string Type { get; set; }=string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Details { get; set; }
        public string TraceId { get; set; } = string.Empty;
        public DateTime Timestamp {  get; set; }
    }
}