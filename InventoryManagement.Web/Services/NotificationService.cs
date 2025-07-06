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

            var content = new StringContent(
                JsonConvert.SerializeObject(new { notificationId }),
                Encoding.UTF8,
                "application/json");

            await _httpClient.PostAsync("/api/notifications/mark-as-read", content);
        }

        public async Task MarkAllAsReadAsync()
        {
            AddAuthorizationHeader();

            var notifications = await GetNotificationsAsync(true);
            foreach (var notification in notifications)
            {
                await MarkAsReadAsync(notification.Id);
            }
        }
    }
}