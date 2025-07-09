using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;

namespace NotificationService.Infrastructure.Services
{
    public class UserService : IUserService
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor? _httpContextAccessor;
        private readonly ILogger<UserService> _logger;
        private readonly IConfiguration _configuration;

        public UserService(
            HttpClient httpClient, 
            IConfiguration configuration, 
            ILogger<UserService> logger,
            IHttpContextAccessor? httpContextAccessor = null)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri(configuration["Services:IdentityService"] ?? "http://localhost:5000");
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<List<UserDto>> GetUsersAsync(string? role = null)
        {
            SetAuthorizationHeader();

            var url = string.IsNullOrEmpty(role)
                ? "/api/auth/users"
                : $"/api/auth/users/by-role/{role}";

            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var users = await response.Content.ReadFromJsonAsync<List<UserDto>>();
                return users ?? new List<UserDto>();
            }
            return new List<UserDto>();
        }

        public async Task<UserDto?> GetUserAsync(int userId)
        {
            SetAuthorizationHeader();

            var response = await _httpClient.GetAsync($"/api/auth/users/{userId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<UserDto>();
            }
            return null;
        }

        public async Task<List<int>> GetUserIdsByRoleAsync(string role, CancellationToken cancellationToken = default)
        {
            try
            {
                // Use system token for background service calls
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", GenerateSystemToken());

                var response = await _httpClient.GetAsync($"/api/auth/users/by-role/{role}", cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var users = await response.Content.ReadFromJsonAsync<List<UserDto>>(cancellationToken: cancellationToken);
                    return users?.Select(u => u.Id).ToList() ?? new List<int>();
                }

                _logger.LogWarning($"Failed to get users for role {role}: {response.StatusCode}");
                return new List<int>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting users for role {role}");
                return new List<int>();
            }
        }
        private string GenerateSystemToken()
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]!);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] {
                new Claim(ClaimTypes.Role, "System"),
                new Claim(ClaimTypes.Name, "System")
            }),
                Expires = DateTime.UtcNow.AddMinutes(5),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"]
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private void SetAuthorizationHeader()
        {
            if (_httpContextAccessor?.HttpContext != null)
            {
                byte[]? tokenBytes;
                if (_httpContextAccessor.HttpContext.Session.TryGetValue("JwtToken", out tokenBytes))
                {
                    var token = System.Text.Encoding.UTF8.GetString(tokenBytes);
                    if (!string.IsNullOrEmpty(token))
                    {
                        _httpClient.DefaultRequestHeaders.Authorization =
                            new AuthenticationHeaderValue("Bearer", token);
                        return;
                    }
                }
            }

            // Fallback to system token
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", "system-token-for-automated-actions");
        }
    }
}