using System.Text;
using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Services.Interfaces;
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

        public async Task<TokenDto?> LoginAsync(string username, string password, bool rememberMe = false)
        {
            try
            {
                // Send login request (backend doesn't need to know about RememberMe)
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
                    var result = JsonConvert.DeserializeObject<TokenDto>(responseContent);

                    if (result != null)
                    {
                        // Set RememberMe from our parameter (frontend decision)
                        result.RememberMe = rememberMe;
                        _logger.LogInformation("Login successful for user: {Username}", username);
                    }

                    return result;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Login failed with status code: {StatusCode}, Response: {Response}",
                    response.StatusCode, errorContent);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error for user: {Username}", username);
                return null;
            }
        }

        public async Task<TokenDto?> RefreshTokenAsync(string refreshToken)
        {
            try
            {
                var refreshDto = new RefreshTokenDto
                {
                    RefreshToken = refreshToken
                };

                var json = JsonConvert.SerializeObject(refreshDto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Don't send authorization header for refresh endpoint
                _httpClient.DefaultRequestHeaders.Authorization = null;

                var response = await _httpClient.PostAsync("api/auth/refresh", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<TokenDto>(responseContent);

                    if (result != null && !string.IsNullOrEmpty(result.AccessToken))
                    {
                        _logger.LogInformation("Token refresh successful");
                        return result;
                    }
                    else
                    {
                        _logger.LogWarning("Token refresh returned invalid data");
                        return null;
                    }
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Token refresh failed with status {Status}: {Content}",
                    response.StatusCode, errorContent);

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
            return Task.FromResult(true);
        }
    }
}