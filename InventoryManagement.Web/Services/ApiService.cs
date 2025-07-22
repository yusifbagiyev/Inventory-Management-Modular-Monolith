using InventoryManagement.Web.Services.Interfaces;
using Newtonsoft.Json;
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
            var token=_httpContextAccessor.HttpContext?.Session.GetString("JwtToken");
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
        
        private async Task<bool> TryRefreshTokenAsync()
        {
            var accessToken = _httpContextAccessor.HttpContext?.Session.GetString("JwtToken");
            var refreshToken = _httpContextAccessor.HttpContext?.Session.GetString("RefreshToken");
            if(string.IsNullOrEmpty(refreshToken)||string.IsNullOrEmpty(accessToken))
            {
                return false;
            }
            
            var tokenDto = await _authService.RefreshTokenAsync(accessToken, refreshToken);

            if(tokenDto != null)
            {
                _httpContextAccessor.HttpContext?.Session.SetString("JwtToken", tokenDto.AccessToken);
                _httpContextAccessor.HttpContext?.Session.SetString("RefreshToken", tokenDto.RefreshToken);
                return true;
            }
            return false;
        }

        public async Task<T?> GetAsync<T>(string endpoint)
        {
            try
            {
                AddAuthorizationHeader();
                var response = await _httpClient.GetAsync(endpoint);

                if (response.StatusCode == HttpStatusCode.Unauthorized && await TryRefreshTokenAsync())
                {
                    AddAuthorizationHeader();
                    response = await _httpClient.GetAsync(endpoint);
                }

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<T>(content);
                }

                // Log error response
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger?.LogError($"API request failed: {response.StatusCode} - {errorContent}");

                return default;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error calling API endpoint: {endpoint}");
                return default;
            }
        }

        public async Task<TResponse?> PostAsync<TRequest,TResponse>(string endpoint, TRequest data)
        {
            try
            {
                AddAuthorizationHeader();

                var json=JsonConvert.SerializeObject(data);
                var content= new StringContent(json, Encoding.UTF8, "application/json");

                var response=await _httpClient.PostAsync(endpoint, content);

                if (response.StatusCode == HttpStatusCode.Unauthorized && await TryRefreshTokenAsync())
                {
                    AddAuthorizationHeader();
                    response = await _httpClient.PostAsync(endpoint, content);
                }

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<TResponse>(responseContent);
                }

                return default;
            }
            catch
            {
                return default;
            }
        }

        public async Task<TResponse?> PostFormAsync<TResponse>(
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

                    content.Add(new StringContent(field.Value), field.Key);
                }

                var response = await _httpClient.PostAsync(endpoint, content);

                if (response.StatusCode == HttpStatusCode.Unauthorized && await TryRefreshTokenAsync())
                {
                    AddAuthorizationHeader();
                    response = await _httpClient.PostAsync(endpoint, content);
                }

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    // If TResponse is string, return the raw content
                    if (typeof(TResponse) == typeof(string))
                    {
                        return (TResponse)(object)responseContent;
                    }
                    return JsonConvert.DeserializeObject<TResponse>(responseContent);
                }

                return default;
            }
            catch
            {
                return default;
            }
        }

        public async Task<TResponse?> PutAsync<TRequest,TResponse>(string endpoint, TRequest data)
        {
            try
            {
                AddAuthorizationHeader();
                var json=JsonConvert.SerializeObject(data);
                var content=new StringContent(json, Encoding.UTF8, "application/json");
                var response=await _httpClient.PutAsync(endpoint, content);

                if (response.StatusCode == HttpStatusCode.Unauthorized && await TryRefreshTokenAsync())
                {
                    AddAuthorizationHeader();
                    response = await _httpClient.PutAsync(endpoint, content);
                }

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<TResponse>(responseContent);
                }

                return default;
            }
            catch
            {
                return default;
            }
        }

        public async Task<TResponse?> PutFormAsync<TResponse>(
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

                if (response.IsSuccessStatusCode)
                {
                    return (TResponse)(object)true;
                }

                return default;
            }
            catch
            {
                return default;
            }
        }

        public async Task<bool> DeleteAsync(string endpoint)
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

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}