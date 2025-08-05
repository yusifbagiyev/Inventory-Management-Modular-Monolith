using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Services.Interfaces;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

namespace InventoryManagement.Web.Services
{
    public class NotificationService : INotificationService
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public NotificationService(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("http://localhost:5000"); // Via API Gateway
            _httpContextAccessor = httpContextAccessor;
        }

        private void AddAuthorizationHeader()
        {
            var token = _httpContextAccessor.HttpContext?.Session.GetString("JwtToken");
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task<List<NotificationDto>> GetNotificationsAsync(bool unreadOnly = false)
        {
            AddAuthorizationHeader();

            var response = await _httpClient.GetAsync($"/api/notifications?unreadOnly={unreadOnly}");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<NotificationDto>>(content) ?? new List<NotificationDto>();
            }
            return new List<NotificationDto>();
        }

        public async Task<int> GetUnreadCountAsync()
        {
            AddAuthorizationHeader();

            var response = await _httpClient.GetAsync("/api/notifications/unread-count");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<int>(content);
            }
            return 0;
        }

        public async Task MarkAsReadAsync(int notificationId)
        {
            AddAuthorizationHeader();

            var response = await _httpClient.PostAsync($"/api/notifications/{notificationId}/mark-as-read", null);
            response.EnsureSuccessStatusCode();
        }

        public async Task MarkAllAsReadAsync()
        {
            AddAuthorizationHeader();

            // Instead of marking individually, use a bulk endpoint
            var response = await _httpClient.PostAsync("/api/notifications/mark-all-read", null);
            response.EnsureSuccessStatusCode();
        }
    }
}