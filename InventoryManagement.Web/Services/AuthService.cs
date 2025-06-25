using System.Text;
using InventoryManagement.Web.Models.DTOs;
using Newtonsoft.Json;

namespace InventoryManagement.Web.Services
{
    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;

        public AuthService(HttpClient httpClient, IConfiguration configuration, ILogger<AuthService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _httpClient.BaseAddress = new Uri(_configuration["ApiGateway:BaseUrl"] ?? "http://localhost:5000");
        }

        public async Task<TokenDto?> LoginAsync(string username, string password)
        {
            try
            {
                var loginDto = new LoginDto
                {
                    Username = username,
                    Password = password
                };

                var json = JsonConvert.SerializeObject(loginDto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("api/auth/login", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<TokenDto>(responseContent);
                }

                _logger.LogWarning($"Login failed with status code: {response.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error");
                return null;
            }
        }

        public async Task<TokenDto?> RefreshTokenAsync(string accessToken, string refreshToken)
        {
            try
            {
                var refreshDto = new RefreshTokenDto
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken
                };

                var json = JsonConvert.SerializeObject(refreshDto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("api/auth/refresh", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<TokenDto>(responseContent);
                }

                _logger.LogWarning($"Token refresh failed with status code: {response.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token refresh error");
                return null;
            }
        }

        public Task<bool> LogoutAsync()
        {
            // Since we're using JWT, logout is handled client-side
            // by removing the token from storage
            return Task.FromResult(true);
        }
    }
}