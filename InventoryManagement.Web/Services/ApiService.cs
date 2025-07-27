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


        private void AddAuthorizationHeader()
        {
            // Clear any existing authorization header
            _httpClient.DefaultRequestHeaders.Authorization = null;

            // First try to get token from HttpContext.Items (set by middleware)
            var token = _httpContextAccessor.HttpContext?.Items["JwtToken"] as string;

            // If not in Items, try session
            if (string.IsNullOrEmpty(token))
            {
                token = _httpContextAccessor.HttpContext?.Session.GetString("JwtToken");
            }

            // If still not found, try cookies (for remember me scenarios)
            if (string.IsNullOrEmpty(token))
            {
                token = _httpContextAccessor.HttpContext?.Request.Cookies["jwt_token"];
            }

            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            else
            {
                _logger.LogWarning("No JWT token found for authorization header");
            }
        }


        private async Task<bool> TryRefreshTokenAsync()
        {
            var accessToken = _httpContextAccessor.HttpContext?.Items["JwtToken"] as string
                           ?? _httpContextAccessor.HttpContext?.Session.GetString("JwtToken")
                           ?? _httpContextAccessor.HttpContext?.Request.Cookies["jwt_token"];

            var refreshToken = _httpContextAccessor.HttpContext?.Items["RefreshToken"] as string
                            ?? _httpContextAccessor.HttpContext?.Session.GetString("RefreshToken")
                            ?? _httpContextAccessor.HttpContext?.Request.Cookies["refresh_token"];

            if (string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("Cannot refresh token - missing access or refresh token");
                return false;
            }

            try
            {
                var tokenDto = await _authService.RefreshTokenAsync(accessToken, refreshToken);

                if (tokenDto != null && !string.IsNullOrEmpty(tokenDto.AccessToken))
                {
                    // Update in HttpContext.Items for immediate use
                    _httpContextAccessor?.HttpContext?.Items["JwtToken"] = tokenDto.AccessToken;
                    _httpContextAccessor?.HttpContext?.Items["RefreshToken"] = tokenDto.RefreshToken;

                    // Update in session
                    _httpContextAccessor?.HttpContext?.Session.SetString("JwtToken", tokenDto.AccessToken);
                    _httpContextAccessor?.HttpContext?.Session.SetString("RefreshToken", tokenDto.RefreshToken);
                    _httpContextAccessor?.HttpContext?.Session.SetString("UserData", JsonConvert.SerializeObject(tokenDto.User));

                    _logger.LogInformation("Token refreshed successfully in ApiService");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token in ApiService");
            }

            return false;
        }


        public async Task<T?> GetAsync<T>(string endpoint)
        {
            try
            {
                AddAuthorizationHeader();
                var response = await _httpClient.GetAsync(endpoint);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    if (await TryRefreshTokenAsync())
                    {
                        AddAuthorizationHeader();
                        response = await _httpClient.GetAsync(endpoint);
                    }
                }

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<T>(content);
                }

                // Log error response
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger?.LogError($"API request failed: {response.StatusCode} - {errorContent}");

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException("Authentication failed");
                }

                return default;
            }
            catch (UnauthorizedAccessException)
            {
                throw; // Re-throw to be handled by calling code
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error calling API endpoint: {endpoint}");
                return default;
            }
        }


        public async Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            try
            {
                AddAuthorizationHeader();

                var json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(endpoint, content);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    if(await TryRefreshTokenAsync())
                    {
                        AddAuthorizationHeader();
                        response = await _httpClient.PostAsync(endpoint, content);
                    }
                }

                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Accepted)
                {
                    // Check if it's an approval response
                    try
                    {
                        dynamic? jsonResponse = JsonConvert.DeserializeObject(responseContent);

                        if (jsonResponse?.Status == "PendingApproval" ||
                            jsonResponse?.status == "PendingApproval" ||
                            response.StatusCode == HttpStatusCode.Accepted)
                        {
                            return new ApiResponse<TResponse>
                            {
                                IsSuccess = false,
                                IsApprovalRequest = true,
                                Message = jsonResponse?.Message ?? "Request submitted for approval",
                                ApprovalRequestId = jsonResponse?.ApprovalRequestId,
                                Data = default
                            };
                        }
                    }
                    catch { }

                    return new ApiResponse<TResponse>
                    {
                        IsSuccess = true,
                        Data = JsonConvert.DeserializeObject<TResponse>(responseContent)
                    };
                }

                return new ApiResponse<TResponse>
                {
                    IsSuccess = false,
                    Message = $"Request failed with status {response.StatusCode}",
                    Data = default
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<TResponse>
                {
                    IsSuccess = false,
                    Message = ex.Message,
                    Data = default
                };
            }
        }


        public async Task<ApiResponse<T>> PostFormAsync<T>(
            string endpoint,
            IFormCollection form,
            object? dataDto=null)
        {
            try
            {
                AddAuthorizationHeader();

                using var content = new MultipartFormDataContent();

                // Add DTO properties first
                if (dataDto != null)
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
                foreach (var file in form.Files)
                {
                    var streamContent = new StreamContent(file.OpenReadStream());
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
                    content.Add(streamContent, "ImageFile", file.FileName);
                }


                // Add remaining form fields (prioritizing DTO values)
                foreach (var field in form)
                {
                    if(field.Key=="ImageFile"||
                        field.Key=="__RequestVerificationToken"||
                        (dataDto?.GetType().GetProperty(field.Key)!=null))
                        continue; // Skip if already handled fields

                    content.Add(new StringContent(field.Value!), field.Key);
                }

                var response = await _httpClient.PostAsync(endpoint, content);

                if (response.StatusCode == HttpStatusCode.Unauthorized && await TryRefreshTokenAsync())
                {
                    AddAuthorizationHeader();
                    response = await _httpClient.PostAsync(endpoint, content);
                }

                var responseContent=await response.Content.ReadAsStringAsync();

                var apiResponse = new ApiResponse<T>();
                if (!response.IsSuccessStatusCode)
                {
                    return new ApiResponse<T>
                    {
                        IsSuccess = false,
                        Message = ParseErrorMessage(responseContent, response.StatusCode),
                        Data = default
                    };
                }

                // Handle successful responses
                try
                {
                    dynamic? jsonResponse = JsonConvert.DeserializeObject(responseContent);

                    // Check for approval responses
                    if (jsonResponse?.status == "PendingApproval" ||
                        jsonResponse?.Status == "PendingApproval" ||
                        jsonResponse?.approvalRequestId != null ||
                        jsonResponse?.ApprovalRequestId != null)
                    {
                        apiResponse.IsApprovalRequest = true;
                        apiResponse.Message = jsonResponse?.message ?? "Request submitted for approval";
                        apiResponse.ApprovalRequestId = jsonResponse?.approvalRequestId ?? jsonResponse?.ApprovalRequestId;
                        apiResponse.IsSuccess = false; // Approval requests aren't "successful" in the traditional sense
                    }
                    else
                    {
                        apiResponse.IsSuccess = true;
                        apiResponse.Data = JsonConvert.DeserializeObject<T>(responseContent);
                    }
                }
                catch
                {
                    apiResponse.IsSuccess = true;
                    if (typeof(T) != typeof(string))
                    {
                        apiResponse.Data = JsonConvert.DeserializeObject<T>(responseContent);
                    }
                }

                return apiResponse;
            }
            catch (Exception ex)
            {
                return new ApiResponse<T>
                {
                    IsSuccess = false,
                    Message = ex.Message
                };
            }
        }


        public async Task<ApiResponse<TResponse>> PutAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            try
            {
                AddAuthorizationHeader();
                var json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync(endpoint, content);

                if (response.StatusCode == HttpStatusCode.Unauthorized && await TryRefreshTokenAsync())
                {
                    AddAuthorizationHeader();
                    response = await _httpClient.PutAsync(endpoint, content);
                }

                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new ApiResponse<TResponse>
                    {
                        IsSuccess = false,
                        Message = ParseErrorMessage(responseContent, response.StatusCode),
                        Data=default
                    };
                }

                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Accepted)
                {
                    // Check for approval response
                    try
                    {
                        dynamic? jsonResponse = JsonConvert.DeserializeObject(responseContent);
                        if (jsonResponse?.Status == "PendingApproval" || response.StatusCode == HttpStatusCode.Accepted)
                        {
                            return new ApiResponse<TResponse>
                            {
                                IsSuccess = false,
                                IsApprovalRequest = true,
                                Message = jsonResponse?.Message ?? "Request submitted for approval",
                                ApprovalRequestId = jsonResponse?.ApprovalRequestId,
                                Data = default
                            };
                        }
                    }
                    catch { }

                    return new ApiResponse<TResponse>
                    {
                        IsSuccess = true,
                        Data = response.StatusCode == HttpStatusCode.NoContent
                            ? (TResponse)(object)true
                            : JsonConvert.DeserializeObject<TResponse>(responseContent)
                    };
                }

                return new ApiResponse<TResponse>
                {
                    IsSuccess = false,
                    Message = $"Request failed with status {response.StatusCode}",
                    Data = default
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<TResponse>
                {
                    IsSuccess = false,
                    Message = ex.Message,
                    Data = default
                };
            }
        }


        public async Task<ApiResponse<TResponse>> PutFormAsync<TResponse>(
            string endpoint,
            IFormCollection form,
            object? dataDto = null)
        {
            try
            {
                AddAuthorizationHeader();
                using var content = new MultipartFormDataContent();

                // Add all form fields
                foreach (var field in form)
                {
                    if (field.Key == "__RequestVerificationToken") continue;
                    content.Add(new StringContent(field.Value.ToString()), field.Key);
                }

                // Add files if any
                foreach (var file in form.Files)
                {
                    if (file.Length > 0)
                    {
                        var streamContent = new StreamContent(file.OpenReadStream());
                        streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
                        content.Add(streamContent, file.Name, file.FileName);
                    }
                }

                var response = await _httpClient.PutAsync(endpoint, content);

                if (response.StatusCode == HttpStatusCode.Unauthorized && await TryRefreshTokenAsync())
                {
                    AddAuthorizationHeader();
                    response = await _httpClient.PutAsync(endpoint, content);
                }

                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new ApiResponse<TResponse>
                    {
                        IsSuccess = false,
                        Message = ParseErrorMessage(responseContent, response.StatusCode),
                        Data = default
                    };
                }

                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Accepted)
                {
                    // Check for approval response
                    try
                    {
                        dynamic? jsonResponse = JsonConvert.DeserializeObject(responseContent);
                        if (jsonResponse?.Status == "PendingApproval" || response.StatusCode == HttpStatusCode.Accepted)
                        {
                            return new ApiResponse<TResponse>
                            {
                                IsSuccess = false,
                                IsApprovalRequest = true,
                                Message = jsonResponse?.Message ?? "Request submitted for approval",
                                ApprovalRequestId = jsonResponse?.ApprovalRequestId,
                                Data = default
                            };
                        }
                    }
                    catch { }

                    return new ApiResponse<TResponse>
                    {
                        IsSuccess = true,
                        Data = response.StatusCode == HttpStatusCode.NoContent
                            ? (TResponse)(object)true
                            : JsonConvert.DeserializeObject<TResponse>(responseContent)
                    };
                }

                return new ApiResponse<TResponse>
                {
                    IsSuccess = response.IsSuccessStatusCode,
                    Data = response.IsSuccessStatusCode ? (TResponse)(object)true : default,
                    Message = response.IsSuccessStatusCode ? null : "Request failed"
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<TResponse>
                {
                    IsSuccess = false,
                    Message = ex.Message,
                    Data = default
                };
            }
        }


        public async Task<ApiResponse<bool>> DeleteAsync(string endpoint)
        {
            try
            {
                AddAuthorizationHeader();
                var response = await _httpClient.DeleteAsync(endpoint);

                if (response.StatusCode == HttpStatusCode.Unauthorized && await TryRefreshTokenAsync())
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
            catch (Exception ex)
            {
                return new ApiResponse<bool>
                {
                    IsSuccess = false,
                    Message = ex.Message,
                    Data = false
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
            catch
            {
                return $"Request failed with status {statusCode}";
            }

            return $"Request failed with status {statusCode}";
        }
    }
}