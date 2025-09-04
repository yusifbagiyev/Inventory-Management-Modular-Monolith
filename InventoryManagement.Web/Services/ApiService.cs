using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Services.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace InventoryManagement.Web.Services
{
    public class ApiService:IApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly IAuthService _authService;
        private readonly ILogger<ApiService> _logger;
        public ApiService(
            HttpClient httpClient,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            IAuthService authService,
            ILogger<ApiService> logger)
        {
            _httpClient = httpClient;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
            _authService = authService;
            _httpClient.BaseAddress = new Uri(_configuration["ApiGateway:BaseUrl"] ?? "http://localhost:5000");
            _logger = logger;
        }

        private async Task<string?> GetValidTokenAsync()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return null;

            // Try to get token from session first, then cookies
            var token = context.Session.GetString("JwtToken")
                ?? context.Request.Cookies["jwt_token"];

            if (string.IsNullOrEmpty(token))
                return null;

            // Check if token needs refresh
            if (ShouldRefreshToken(token))
            {
                var refreshToken = context.Session.GetString("RefreshToken")
                    ?? context.Request.Cookies["refresh_token"];

                if (!string.IsNullOrEmpty(refreshToken))
                {
                    var newToken = await RefreshTokenAsync(token, refreshToken);
                    if (!string.IsNullOrEmpty(newToken))
                    {
                        UpdateStoredTokens(newToken, refreshToken);
                        return newToken;
                    }
                }

                return null; // Token expired and couldn't refresh
            }

            return token;
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

                return timeUntilExpiry.TotalMinutes < 5;
            }
            catch
            {
                return true;
            }
        }

        private async Task<string?> RefreshTokenAsync(string accessToken, string refreshToken)
        {
            try
            {
                var refreshDto = new { AccessToken = accessToken, RefreshToken = refreshToken };
                var json = JsonConvert.SerializeObject(refreshDto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("api/auth/refresh", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var tokenResponse = JsonConvert.DeserializeObject<TokenDto>(responseContent);
                    return tokenResponse?.AccessToken;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
            }

            return null;
        }

        private void UpdateStoredTokens(string accessToken, string refreshToken)
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            // Update session
            context.Session.SetString("JwtToken", accessToken);
            context.Session.SetString("RefreshToken", refreshToken);

            // Update cookies if they exist
            if (context.Request.Cookies.ContainsKey("jwt_token"))
            {
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = context.Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.Now.AddDays(30)
                };

                context.Response.Cookies.Append("jwt_token", accessToken, cookieOptions);
                context.Response.Cookies.Append("refresh_token", refreshToken, cookieOptions);
            }
        }

        private async Task AddAuthorizationHeaderAsync()
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;

            var token = await GetValidTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            else
            {
                // Token is invalid and couldn't be refreshed - user needs to login again
                throw new UnauthorizedAccessException("Authentication token is invalid. Please login again.");
            }
        }

        public async Task<T?> GetAsync<T>(string endpoint)
        {
            await AddAuthorizationHeaderAsync();

            var response = await _httpClient.GetAsync(endpoint);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<T>(content);
            }

            await HandleErrorResponse(response);
            return default;
        }


        public async Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            await AddAuthorizationHeaderAsync();

            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(endpoint, content);

            var responseContent = await response.Content.ReadAsStringAsync();

            return await ProcessResponse<TResponse>(response, responseContent);
        }


        public async Task<ApiResponse<T>> PostFormAsync<T>(
            string endpoint,
            IFormCollection form,
            object? dataDto=null)
        {
            await AddAuthorizationHeaderAsync();

            using var content = BuildMultipartContent(form, dataDto);

            var response = await _httpClient.PostAsync(endpoint, content);


            var responseContent = await response.Content.ReadAsStringAsync();

            return await ProcessResponse<T>(response, responseContent);
        }


        public async Task<ApiResponse<TResponse>> PutAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            await AddAuthorizationHeaderAsync();

            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(endpoint, content);

            var responseContent = await response.Content.ReadAsStringAsync();

            return await ProcessResponse<TResponse>(response, responseContent);
        }


        public async Task<ApiResponse<TResponse>> PutFormAsync<TResponse>(
            string endpoint,
            IFormCollection form,
            object? dataDto = null)
        {
            await AddAuthorizationHeaderAsync();

            using var content = BuildMultipartContent(form, dataDto);

            var response = await _httpClient.PutAsync(endpoint, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            return await ProcessResponse<TResponse>(response, responseContent);
        }


        public async Task<ApiResponse<bool>> DeleteAsync(string endpoint)
        {
            await AddAuthorizationHeaderAsync();

            var response = await _httpClient.DeleteAsync(endpoint);

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
        private async Task HandleErrorResponse(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();
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