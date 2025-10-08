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
        private readonly ITokenManager _tokenManager;

        // Track if we've already attempted refresh for this request
        private readonly string REQUEST_REFRESH_KEY = "TokenRefreshAttempted";
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
            _tokenManager = tokenRefreshService;
            _logger = logger;
            _httpClient.BaseAddress = new Uri(_configuration["ApiGateway:BaseUrl"] ?? "http://localhost:5000");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }


        private void SetClientIpHeaders()
        {
            var context= _httpContextAccessor.HttpContext;
            if (context == null) return;

            var clientIp = context.Connection.RemoteIpAddress?.ToString();

            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();

            if (!string.IsNullOrEmpty(forwardedFor))
            {
                // Nginx already set this - pass it along
                _httpClient.DefaultRequestHeaders.Remove("X-Forwarded-For");
                _httpClient.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);

                // Also set X-Real-IP to the first IP in the chain
                var realIp = forwardedFor.Split(',')[0].Trim();
                _httpClient.DefaultRequestHeaders.Remove("X-Real-IP");
                _httpClient.DefaultRequestHeaders.Add("X-Real-IP", realIp);
                _logger.LogDebug("Forwarding client IP headers - X-Forwarded-For: {ForwardedFor}, X-Real-IP: {RealIp}",
                    forwardedFor, realIp);
            }
            else if (!string.IsNullOrEmpty(clientIp))
            {
                // Start the X-Forwarded-For chain
                _httpClient.DefaultRequestHeaders.Remove("X-Forwarded-For");
                _httpClient.DefaultRequestHeaders.Add("X-Forwarded-For", clientIp);

                _httpClient.DefaultRequestHeaders.Remove("X-Real-IP");
                _httpClient.DefaultRequestHeaders.Add("X-Real-IP", clientIp);

                _logger.LogDebug("Starting X-Forwarded-For chain with client IP: {ClientIp}", clientIp);
            }
        }


        private bool SetAuthorizationHeader()
        {
            // Clear any existing authorization header
            _httpClient.DefaultRequestHeaders.Authorization = null;

            try
            {
                var context = _httpContextAccessor.HttpContext;

                // SECURITY: Token is only available from HttpContext.Items (set by middleware)
                // or from session - never from JavaScript
                var token = context?.Items["JwtToken"] as string;

                if (string.IsNullOrEmpty(token))
                {
                    // Fallback to session if not in Items
                    token = context?.Session.GetString("JwtToken");
                }

                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", token);
                    _logger.LogDebug("Authorization header set successfully");
                    return true;
                }

                _logger.LogWarning("No valid token available for authorization");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting authorization header");
                return false;
            }
        }



        public async Task<T?> GetAsync<T>(string endpoint)
        {
            // Ensure we have a valid token
            if (!SetAuthorizationHeader())
            {
                _logger.LogWarning("Cannot proceed without valid token for {Endpoint}", endpoint);
                throw new UnauthorizedAccessException("Authentication required");
            }

            SetClientIpHeaders();

            var response = await _httpClient.GetAsync(endpoint);

            // Handle 401 with single retry after refresh
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var context = _httpContextAccessor.HttpContext;

                // Check if we've already tried refreshing for this request
                if (context?.Items.ContainsKey(REQUEST_REFRESH_KEY) != true)
                {
                    _logger.LogInformation("Received 401, attempting token refresh for {Endpoint}", endpoint);

                    // Mark that we've attempted refresh
                    if (context != null)
                    {
                        context.Items[REQUEST_REFRESH_KEY] = true;
                    }

                    // Try to refresh
                    var refreshed = await _tokenManager.RefreshTokenAsync();

                    if (refreshed && SetAuthorizationHeader())
                    {
                        // Retry the request with new token
                        response = await _httpClient.GetAsync(endpoint);

                        if (response.IsSuccessStatusCode)
                        {
                            var retryContent = await response.Content.ReadAsStringAsync();
                            return JsonConvert.DeserializeObject<T>(retryContent);
                        }
                    }
                }

                _logger.LogError("Authentication failed for {Endpoint} after refresh attempt", endpoint);
                throw new UnauthorizedAccessException("Authentication failed - please login again");
            }

            

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<T>(content);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("API request failed: {StatusCode} - {Content} for {Endpoint}",
                    response.StatusCode, errorContent, endpoint);
                return default(T);
            }
        }



        public async Task<ApiResponse<T>> PostAsync<T>(string endpoint, object? data)
        {
            if(data == null)
            {
                return new ApiResponse<T>
                {
                    IsSuccess = false,
                    Message = "Data must not be  null"
                };
            }

            if(!SetAuthorizationHeader())
            {
                return new ApiResponse<T>
                {
                    IsSuccess = false,
                    Message = "Authentication required - please login"
                };
            }

            SetClientIpHeaders();
            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(endpoint, content);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var context=_httpContextAccessor.HttpContext;

                if(context?.Items.ContainsKey(REQUEST_REFRESH_KEY)!=true)
                {
                    _logger.LogInformation("Received 401 on POST, attempting refresh for {Endpoint}", endpoint);

                    if(context!=null)
                    {
                        context.Items[REQUEST_REFRESH_KEY] = true;
                    }

                    var refreshed = await _tokenManager.RefreshTokenAsync();
                    
                    if(refreshed&& SetAuthorizationHeader())
                    {
                        response=await _httpClient.PostAsync(endpoint, content);
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

            // For non-success responses, return structured error
            return await ProcessResponse<T>(response, responseContent);
        }



        public async Task<ApiResponse<T>> PostFormAsync<T>(
            string endpoint,
            IFormCollection form,
            object? dataDto=null)
        {
            if(! SetAuthorizationHeader())
            {
                return new ApiResponse<T>
                {
                    IsSuccess = false,
                    Message = "Authentication required - please login"
                };
            }
            SetClientIpHeaders();


            using var content = BuildMultipartContent(form, dataDto);

            var response = await _httpClient.PostAsync(endpoint, content);

            if(response.StatusCode==HttpStatusCode.Unauthorized)
            {
                var context=_httpContextAccessor.HttpContext;
                if(context?.Items.ContainsKey(REQUEST_REFRESH_KEY)!=true)
                {
                    _logger.LogInformation("Received 401 on POST, attempting refresh for {Endpoint}", endpoint);

                    if (context != null)
                    {
                        context.Items[REQUEST_REFRESH_KEY] = true;
                    }

                    var refreshed=await _tokenManager.RefreshTokenAsync();

                    if(refreshed &&  SetAuthorizationHeader())
                    {
                        response=await _httpClient.PostAsync(endpoint, content);
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



        public async Task<ApiResponse<T>> PutAsync<T>(string endpoint, object? data)
        {
            if (data == null)
            {
                return new ApiResponse<T>
                {
                    IsSuccess = false,
                    Message = "Data must not be null"
                };
            }

            if(! SetAuthorizationHeader())
            {
                return new ApiResponse<T>
                {
                    IsSuccess = false,
                    Message = "Authentication required - please login"
                };
            }
            SetClientIpHeaders();


            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(endpoint, content);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var context = _httpContextAccessor.HttpContext;

                if(context?.Items.ContainsKey(REQUEST_REFRESH_KEY)!=true)
                {
                    _logger.LogInformation("Received 401 on PUT, attempting refresh for {Endpoint}", endpoint);

                    if (context != null)
                    {
                        context.Items[REQUEST_REFRESH_KEY] = true;
                    }

                    var refreshed = await _tokenManager.RefreshTokenAsync();

                    if (refreshed &&  SetAuthorizationHeader())
                    {
                        response = await _httpClient.PutAsync(endpoint, content);
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
            }

            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // Check if response indicates approval even with 200 OK
                if (IsApprovalResponse(responseContent))
                {
                    return HandleApprovalResponse<T>(responseContent);
                }


                if (string.IsNullOrEmpty(responseContent))
                {
                    return new ApiResponse<T>
                    {
                        IsSuccess = true
                    };
                }

                return new ApiResponse<T>
                {
                    IsSuccess = true,
                    Data = JsonConvert.DeserializeObject<T>(responseContent)
                };
            }

            return await ProcessResponse<T>(response, responseContent);
        }



        public async Task<ApiResponse<TResponse>> PutFormAsync<TResponse>(
            string endpoint,
            IFormCollection form,
            object? dataDto = null)
        {
            if(! SetAuthorizationHeader())
            {
                return new ApiResponse<TResponse>
                {
                    IsSuccess = false,
                    Message = "Authentication required - please login"
                };
            }
            SetClientIpHeaders();

            using var content = BuildMultipartContent(form, dataDto);

            var response = await _httpClient.PutAsync(endpoint, content);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var context=_httpContextAccessor.HttpContext;

                if(context?.Items.ContainsKey(REQUEST_REFRESH_KEY)!=true)
                {
                    _logger.LogInformation("Received 401 on PUT, attempting refresh for {Endpoint}", endpoint);
                    if (context != null)
                    {
                        context.Items[REQUEST_REFRESH_KEY] = true;
                    }
                    var refreshed = await _tokenManager.RefreshTokenAsync();
                    if (refreshed &&  SetAuthorizationHeader())
                    {
                        response = await _httpClient.PutAsync(endpoint, content);
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
            }

            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // Check if response indicates approval even with 200 OK
                if (IsApprovalResponse(responseContent))
                {
                    return HandleApprovalResponse<TResponse>(responseContent);
                }
                if (string.IsNullOrEmpty(responseContent))
                {
                    return new ApiResponse<TResponse>
                    {
                        IsSuccess = true
                    };
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
            if(! SetAuthorizationHeader())
            {
                return new ApiResponse<bool>
                {
                    IsSuccess = false,
                    Message = "Authentication required - please login"
                };
            }
            SetClientIpHeaders();

            var response = await _httpClient.DeleteAsync(endpoint);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var context= _httpContextAccessor.HttpContext;

                if(context?.Items.ContainsKey(REQUEST_REFRESH_KEY)!=true)
                {
                    _logger.LogInformation("Received 401, attempting token refresh for DELETE {Endpoint}", endpoint);

                    if (context != null)
                    {
                        context.Items[REQUEST_REFRESH_KEY] = true;
                    }

                    var refreshed = await _tokenManager.RefreshTokenAsync();

                    if (refreshed &&  SetAuthorizationHeader())
                    {
                        response = await _httpClient.DeleteAsync(endpoint);
                    }

                    else
                    {
                        throw new UnauthorizedAccessException("Authentication failed - please login again");
                    }
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
    }
}