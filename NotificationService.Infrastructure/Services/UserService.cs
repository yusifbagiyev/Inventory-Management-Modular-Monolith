using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using NotificationService.Application.Interfaces;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace NotificationService.Infrastructure.Services
{
    public class UserService : IUserService
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor? _httpContextAccessor;

        public UserService(HttpClient httpClient, IConfiguration configuration, IHttpContextAccessor? httpContextAccessor = null)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri(configuration["Services:IdentityService"] ?? "http://localhost:5000");
            _httpContextAccessor = httpContextAccessor;
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
            SetAuthorizationHeader();

            var response = await _httpClient.GetAsync($"/api/auth/users/by-role/{role}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var users = await response.Content.ReadFromJsonAsync<List<Application.Interfaces.UserDto>>(cancellationToken: cancellationToken);
                return users?.Select(u => u.Id).ToList() ?? new List<int>();
            }
            return new List<int>();
        }

        private void SetAuthorizationHeader()
        {
            if (_httpContextAccessor?.HttpContext != null)
            {
                var token = _httpContextAccessor.HttpContext.Session.GetString("JwtToken");
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", token);
                    return;
                }
            }

            // Use system token for internal service calls
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", "system-token-for-automated-actions");
        }
    }
    public record UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
    }
}