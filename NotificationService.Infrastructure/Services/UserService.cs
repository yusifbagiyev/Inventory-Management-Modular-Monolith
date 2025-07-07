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
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserService(HttpClient httpClient, IConfiguration configuration, IHttpContextAccessor? httpContextAccessor = null)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri(configuration["Services:IdentityService"] ?? "http://localhost:5000");
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<IEnumerable<int>> GetUserIdsByRoleAsync(string role, CancellationToken cancellationToken = default)
        {
            // Use system token for internal service calls
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", "system-token-for-automated-actions");

            var response = await _httpClient.GetAsync($"/api/auth/users/by-role/{role}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var users = await response.Content.ReadFromJsonAsync<List<UserDto>>(cancellationToken: cancellationToken);
                return users?.Where(u => u.Roles.Contains(role)).Select(u => u.Id) ?? new List<int>();
            }
            return new List<int>();
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