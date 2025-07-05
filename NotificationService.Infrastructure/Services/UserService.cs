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

        public UserService(HttpClient httpClient, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri(configuration["Services:IdentityService"] ?? "http://localhost:5000");
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<IEnumerable<int>> GetUserIdsByRoleAsync(string role, CancellationToken cancellationToken = default)
        {
            var authHeader = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", authHeader.Replace("Bearer ", ""));
            }

            var response = await _httpClient.GetAsync($"/api/users/by-role/{role}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var userIds = await response.Content.ReadFromJsonAsync<List<int>>(cancellationToken: cancellationToken);
                return userIds ?? new List<int>();
            }
            return new List<int>();
        }
    }
}