using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Services.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace InventoryManagement.Web.Services
{
    public class ApiService:IApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ApiService> _logger;
        private readonly ITokenRefreshService _tokenRefreshService;
        public ApiService(
            HttpClient httpClient,
            IHttpContextAccessor httpContextAccessor,
            ITokenRefreshService tokenRefreshService,
            IConfiguration configuration,
            ILogger<ApiService> logger)
        {
            _httpClient = httpClient;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
            _tokenRefreshService = tokenRefreshService;
            _httpClient.BaseAddress = new Uri(_configuration["ApiGateway:BaseUrl"] ?? "http://localhost:5000");
            _logger = logger;
        }


        private void AddAuthorizationHeader()
        {
            // Clear any existing authorization header
            _httpClient.DefaultRequestHeaders.Authorization = null;

            // First try to get token from HttpContext.Items (set by middleware)
            var token = _httpContextAccessor.HttpContext?.Items["JwtToken"] as string
                ?? _httpContextAccessor.HttpContext?.Session.GetString("JwtToken")
                ?? _httpContextAccessor.HttpContext?.Request.Cookies["jwt_token"];

            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                _logger.LogDebug("Authorization header set with token");
            }
            else
            {
                _logger.LogWarning("No JWT token found for authorization header");
            }
        }


        private string? GetCurrentAccessToken()
        {
            return _httpContextAccessor.HttpContext?.Items["JwtToken"] as string
                ?? _httpContextAccessor.HttpContext?.Session.GetString("JwtToken")
                ?? _httpContextAccessor.HttpContext?.Request.Cookies["jwt_token"];
        }

        private string? GetCurrentRefreshToken()
        {
            return _httpContextAccessor.HttpContext?.Items["RefreshToken"] as string
                ?? _httpContextAccessor.HttpContext?.Session.GetString("RefreshToken")
                ?? _httpContextAccessor.HttpContext?.Request.Cookies["refresh_token"];
        }

        private void StoreTokens(TokenDto tokenDto)
        {
            var context=_httpContextAccessor.HttpContext;
            if (context == null) return;

            // Update in HttpContext.Items for immediate use
            context.Items["JwtToken"]=tokenDto.AccessToken;
            context.Items["RefreshToken"]= tokenDto.RefreshToken;

            // Update in session
            context.Session.SetString("JwtToken",tokenDto.AccessToken);
            context.Session.SetString("RefreshToken", tokenDto.RefreshToken);
            context.Session.SetString("UserData", JsonConvert.SerializeObject(tokenDto.User));

            // Update cookies if they exists (Remember me)
            if (context.Request.Cookies.ContainsKey("jwt_token"))
            {
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = context.Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.Now.AddDays(30)
                };

                context.Response.Cookies.Append("jwt_token",tokenDto.AccessToken, cookieOptions);
                context.Response.Cookies.Append("refresh_token",tokenDto.RefreshToken,cookieOptions);
                context.Response.Cookies.Append("user_data",JsonConvert.SerializeObject(tokenDto.User),cookieOptions);
            }
        }


        public async Task<T?> GetAsync<T>(string endpoint)
        {
            AddAuthorizationHeader();

            var response = await _httpClient.GetAsync(endpoint);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogInformation("Received 401, attempting token refresh for GET {Endpoint}", endpoint);

                var refreshResult = await _tokenRefreshService.RefreshTokenIfNeededAsync();

                if (refreshResult != null)
                {
                    AddAuthorizationHeader();
                    response = await _httpClient.GetAsync(endpoint);
                }
                else
                {
                    throw new UnauthorizedAccessException("Authentication failed - unable to refresh token");
                }
            }

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<T>(content);
            }

            // Log error response
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("API request failed: {StatusCode} - {ErrorContent} for endpoint: {Endpoint}",
            response.StatusCode, errorContent, endpoint);

            // Let the middleware handle non-success status codes
            await ThrowHttpRequestException(response, errorContent);
            return default; // This line won't be reached but satisfies compiler
        }


        public async Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            AddAuthorizationHeader();

            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(endpoint, content);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var refreshResult = await _tokenRefreshService.RefreshTokenIfNeededAsync();

                if (refreshResult != null)
                {
                    AddAuthorizationHeader();
                    response = await _httpClient.PostAsync(endpoint, content);
                }
                else
                {
                    return new ApiResponse<TResponse>
                    {
                        IsSuccess = false,
                        Message = "Authentication failed - please login again"
                    };
                }
            }

            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // Check if response indicates approval even with 200 OK
                if (IsApprovalResponse(responseContent))
                {
                    return HandleApprovalResponse<TResponse>(responseContent);
                }

                return new ApiResponse<TResponse>
                {
                    IsSuccess = true,
                    Data = JsonConvert.DeserializeObject<TResponse>(responseContent)
                };
            }

            // For non-success responses, return structured error
            return new ApiResponse<TResponse>
            {
                IsSuccess = false,
                Message = ParseErrorMessage(responseContent, response.StatusCode),
                Data = default
            };
        }


        public async Task<ApiResponse<T>> PostFormAsync<T>(
            string endpoint,
            IFormCollection form,
            object? dataDto=null)
        {
            AddAuthorizationHeader();

            using var content = BuildMultipartContent(form, dataDto);

            var response = await _httpClient.PostAsync(endpoint, content);

            var refreshResult = await _tokenRefreshService.RefreshTokenIfNeededAsync();

            if (refreshResult != null)
            {
                AddAuthorizationHeader();
                response = await _httpClient.PostAsync(endpoint, content);
            }

            var responseContent = await response.Content.ReadAsStringAsync();

            return await ProcessResponse<T>(response, responseContent);
        }


        public async Task<ApiResponse<TResponse>> PutAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            AddAuthorizationHeader();

            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(endpoint, content);

            var refreshResult = await _tokenRefreshService.RefreshTokenIfNeededAsync();

            if (refreshResult != null)
            {
                AddAuthorizationHeader();
                response = await _httpClient.PutAsync(endpoint, content);
            }

            var responseContent = await response.Content.ReadAsStringAsync();

            return await ProcessResponse<TResponse>(response, responseContent);
        }


        public async Task<ApiResponse<TResponse>> PutFormAsync<TResponse>(
            string endpoint,
            IFormCollection form,
            object? dataDto = null)
        {
            AddAuthorizationHeader();

            using var content = BuildMultipartContent(form, dataDto);

            var response = await _httpClient.PutAsync(endpoint, content);

            var refreshResult = await _tokenRefreshService.RefreshTokenIfNeededAsync();

            if (refreshResult != null)
            {
                AddAuthorizationHeader();
                response = await _httpClient.PutAsync(endpoint, content);
            }

            var responseContent = await response.Content.ReadAsStringAsync();

            return await ProcessResponse<TResponse>(response, responseContent);
        }


        public async Task<ApiResponse<bool>> DeleteAsync(string endpoint)
        {
            AddAuthorizationHeader();

            var response = await _httpClient.DeleteAsync(endpoint);

            var refreshResult = await _tokenRefreshService.RefreshTokenIfNeededAsync();

            if (refreshResult != null)
            {
                AddAuthorizationHeader();
                response = await _httpClient.DeleteAsync(endpoint);
            }

            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                dynamic? jsonResponse = JsonConvert.DeserializeObject(responseContent);

                return new ApiResponse<bool>
                {
                    IsSuccess = false,
                    IsApprovalRequest = true,
                    Message = jsonResponse?.Message ?? "Request submitted for approval",
                    ApprovalRequestId = jsonResponse?.ApprovalRequestId,
                    Data = false
                };
            }

            return new ApiResponse<bool>
            {
                IsSuccess = response.IsSuccessStatusCode,
                Data = response.IsSuccessStatusCode,
                Message = response.IsSuccessStatusCode ? null : $"Request failed with status {response.StatusCode}"
            };
        }


        private MultipartFormDataContent BuildMultipartContent(IFormCollection form,object? dataDto)
        {
            var content= new MultipartFormDataContent();

            // Add DTO properties first
            if(dataDto != null)
            {
                var properties = dataDto.GetType().GetProperties();
                foreach (var prop in properties)
                {
                    if (prop.Name == "ImageFile") continue;

                    var value = prop.GetValue(dataDto)?.ToString() ?? "";
                    content.Add(new StringContent(value), prop.Name);
                }
            }

            // Add form files
            foreach(var file in form.Files)
            {
                if (file.Length > 0)
                {
                    var streamContent=new StreamContent(file.OpenReadStream());
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
                    content.Add(streamContent, file.Name, file.FileName);
                }
            }

            // Add remaining form fields
            foreach(var field in form)
            {
                if(field.Key=="ImageFile"||
                    field.Key=="__RequestVerificationToken"||
                    (dataDto?.GetType().GetProperty(field.Key) != null))
                    continue;

                content.Add(new StringContent(field.Value!),field.Key);
            }
            return content;
        }


        private async Task<ApiResponse<T>> ProcessResponse<T>(HttpResponseMessage response, string responseContent)
        {
            // Handle approval responses
            if (response.StatusCode == HttpStatusCode.Accepted|| IsApprovalResponse(responseContent))
            {
                return HandleApprovalResponse<T>(responseContent);
            }
            if (!response.IsSuccessStatusCode)
            {
                return new ApiResponse<T>
                {
                    IsSuccess = false,
                    Message = ParseErrorMessage(responseContent, response.StatusCode),
                    Data = default
                };
            }

            // Handle NoContent responses
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return new ApiResponse<T>
                {
                    IsSuccess = true,
                    Data = typeof(T) == typeof(bool) ? (T)(object)true : default
                };
            }

            return new ApiResponse<T>
            {
                IsSuccess = true,
                Data = JsonConvert.DeserializeObject<T>(responseContent)
            };
        }

        private bool IsApprovalResponse(string responseContent)
        {
            try
            {
                dynamic? jsonResponse = JsonConvert.DeserializeObject(responseContent);
                return jsonResponse?.Status=="PendingApproval"||
                       jsonResponse?.status=="PendingApproval"||
                       jsonResponse?.approvalRequestId!=null||
                       jsonResponse?.ApprovalRequestId!=null;
            }
            catch
            {
                return false;
            }
        }

        private ApiResponse<T> HandleApprovalResponse<T> (string responseContent)
        {
            try
            {
                dynamic? jsonResponse= JsonConvert.DeserializeObject(responseContent);
                return new ApiResponse<T>
                {
                    IsSuccess = false,
                    IsApprovalRequest = true,
                    Message = jsonResponse?.Message ?? jsonResponse?.message ?? "Request submitted for approval",
                    ApprovalRequestId = jsonResponse?.ApprovalRequestId ?? jsonResponse?.approvalRequestId,
                    Data=default
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing approval response");
                return new ApiResponse<T>
                {
                    IsSuccess = false,
                    IsApprovalRequest = true,
                    Message = "Request submitted for approval",
                    Data = default
                };
            }
        }

        private string ParseErrorMessage(string responseContent, HttpStatusCode statusCode)
        {
            try
            {
                dynamic? errorResponse = JsonConvert.DeserializeObject(responseContent);

                if (errorResponse?.error != null)
                    return errorResponse.error.ToString();

                if (errorResponse?.message != null)
                    return errorResponse.message.ToString();

                if (errorResponse?.errors != null)
                {
                    var errors = new List<string>();
                    foreach (var error in errorResponse.errors)
                    {
                        if (error.Value is JArray array)
                        {
                            foreach (var item in array)
                                errors.Add(item.ToString());
                        }
                        else
                        {
                            errors.Add(error.Value.ToString());
                        }
                    }
                    return string.Join("; ", errors);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing error message from response");
            }
            // Return status-based default message
            return GetDefaultErrorMessage(statusCode);
        }

        private string GetDefaultErrorMessage(HttpStatusCode statusCode)
        {
            return statusCode switch
            {
                HttpStatusCode.BadRequest => "Invalid request. Please check your input.",
                HttpStatusCode.Unauthorized => "You are not authorized. Please login again.",
                HttpStatusCode.Forbidden => "You don't have permission to perform this action.",
                HttpStatusCode.NotFound => "The requested resource was not found.",
                HttpStatusCode.Conflict => "This operation conflicts with existing data.",
                HttpStatusCode.InternalServerError => "Server error occurred. Please try again later.",
                _ => $"Request failed with status {statusCode}"
            };
        }

        // Throw appropriate exception based on HTTP status
        private Task ThrowHttpRequestException(HttpResponseMessage response, string content)
        {
            var message = ParseErrorMessage(content, response.StatusCode);

            switch (response.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                    throw new UnauthorizedAccessException(message);
                case HttpStatusCode.Forbidden:
                    throw new UnauthorizedAccessException(message);
                case HttpStatusCode.NotFound:
                    throw new KeyNotFoundException(message);
                case HttpStatusCode.Conflict:
                    throw new InvalidOperationException(message);
                default:
                    throw new HttpRequestException(message);
            }
        }
    }
}