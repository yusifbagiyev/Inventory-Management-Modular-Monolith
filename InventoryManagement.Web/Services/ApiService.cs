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
        private readonly ITokenManager _tokenRefreshService;
        public ApiService(
            HttpClient httpClient,
            IHttpContextAccessor httpContextAccessor,
            ITokenManager tokenRefreshService,
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


        private async Task AddAuthorizationHeader()
        {
            // Clear any existing authorization header
            _httpClient.DefaultRequestHeaders.Authorization = null;

            // First try to get token from HttpContext.Items (set by middleware)
            var token = _httpContextAccessor.HttpContext?.Items["JwtToken"] as string
                ?? _httpContextAccessor.HttpContext?.Session.GetString("JwtToken")
                ?? _httpContextAccessor.HttpContext?.Request.Cookies["jwt_token"];

            // If still no token but user is authenticated, try to get a valid token
            if (string.IsNullOrEmpty(token) && _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true)
            {
                try
                {
                    using var scope = _httpContextAccessor.HttpContext.RequestServices.CreateScope();
                    var tokenManager = scope.ServiceProvider.GetRequiredService<ITokenManager>();

                    token = await tokenManager.GetValidTokenAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to get valid token from TokenManager");
                }
            }

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



        public async Task<T?> GetAsync<T>(string endpoint)
        {
            await AddAuthorizationHeader();

            var response = await _httpClient.GetAsync(endpoint);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogInformation("Received 401, attempting token refresh for GET {Endpoint}", endpoint);

                var refreshResult = await _tokenRefreshService.RefreshTokenAsync();

                if (refreshResult)
                {
                    await AddAuthorizationHeader();
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
            await AddAuthorizationHeader();

            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(endpoint, content);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var refreshResult = await _tokenRefreshService.RefreshTokenAsync();

                if (refreshResult)
                {
                    await AddAuthorizationHeader();
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
            return await ProcessResponse<TResponse>(response, responseContent);
        }



        public async Task<ApiResponse<T>> PostFormAsync<T>(
            string endpoint,
            IFormCollection form,
            object? dataDto=null)
        {
            await AddAuthorizationHeader();

            using var content = BuildMultipartContent(form, dataDto);

            var response = await _httpClient.PostAsync(endpoint, content);

            if(response.StatusCode==HttpStatusCode.Unauthorized)
            {
                _logger.LogInformation("Received 401, attempting token refresh for POST {Endpoint}", endpoint);

                var refreshResult = await _tokenRefreshService.RefreshTokenAsync();

                if (refreshResult)
                {
                    await AddAuthorizationHeader();
                    response = await _httpClient.PostAsync(endpoint, content);
                }
                else
                {
                    return new ApiResponse<T>
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
                    return HandleApprovalResponse<T>(responseContent);
                }

                return new ApiResponse<T>
                {
                    IsSuccess = true,
                    Data = JsonConvert.DeserializeObject<T>(responseContent)
                };
            }

            return await ProcessResponse<T>(response, responseContent);
        }



        public async Task<ApiResponse<TResponse>> PutAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            await AddAuthorizationHeader();

            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(endpoint, content);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {   
                _logger.LogInformation("Received 401, attempting token refresh for PUT {Endpoint}", endpoint);
                var refreshResult = await _tokenRefreshService.RefreshTokenAsync();

                if (refreshResult)
                {
                    await AddAuthorizationHeader();
                    response = await _httpClient.PutAsync(endpoint, content);
                }
                else
                {
                    throw new UnauthorizedAccessException("Authentication failed - unable to refresh token");
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

            return await ProcessResponse<TResponse>(response, responseContent);
        }



        public async Task<ApiResponse<TResponse>> PutFormAsync<TResponse>(
            string endpoint,
            IFormCollection form,
            object? dataDto = null)
        {
            await AddAuthorizationHeader();

            using var content = BuildMultipartContent(form, dataDto);

            var response = await _httpClient.PutAsync(endpoint, content);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogInformation("Received 401, attempting token refresh for PUT {Endpoint}", endpoint);
                var refreshResult = await _tokenRefreshService.RefreshTokenAsync();

                if (refreshResult)
                {
                    await AddAuthorizationHeader();
                    response = await _httpClient.PutAsync(endpoint, content);
                }
                else
                {
                    throw new UnauthorizedAccessException("Authentication failed - unable to refresh token");
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

            return await ProcessResponse<TResponse>(response, responseContent);
        }



        public async Task<ApiResponse<bool>> DeleteAsync(string endpoint)
        {
            await AddAuthorizationHeader();

            var response = await _httpClient.DeleteAsync(endpoint);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogInformation("Received 401, attempting token refresh for DELETE {Endpoint}", endpoint);
                var refreshResult = await _tokenRefreshService.RefreshTokenAsync();

                if (refreshResult)
                {
                    await AddAuthorizationHeader();
                    response = await _httpClient.DeleteAsync(endpoint);
                }
                else
                {
                    throw new UnauthorizedAccessException("Authentication failed - unable to refresh token");
                }
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

            await Task.Delay(1); // Simulate async work if needed

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