using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;

namespace NotificationService.Infrastructure.Services
{
    public class WhatsAppService:IWhatsAppService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WhatsAppService> _logger;
        private readonly WhatsAppSettings _settings;

        public WhatsAppService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<WhatsAppService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            _settings = configuration.GetSection("Whatsapp").Get<WhatsAppSettings>()
                ?? throw new InvalidOperationException("Whatsapp settings not found in configuration");

            _httpClient.BaseAddress = new Uri(_settings.ApiUrl);
        }

        public async Task<bool> SendGroupMessageAsync(string groupId,string message)
        {
            try
            {
                // Ensure group Id is in the correct format
                if (!groupId.EndsWith("@g.us"))
                    groupId = $"{groupId}@g.us";

                // Create the request payload according to Green API documentation
                var payload = new
                {
                    chatId = groupId,
                    message
                };

                // Construct the API endpoint URL
                var endpoint = $"/waInstance{_settings.IdInstance}/sendMessage/{_settings.ApiTokenInstance}";

                // Send the HTTP request
                var response = await SendRequestAsync(endpoint, payload);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Whatsapp message sent succesfully to group {groupId}");
                    return true;
                }
                var errorContent=await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to send WhatsApp message. Status: {response.StatusCode}, Error: {errorContent}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending WhatsApp message to group {groupId}");
                return false;
            }
        }


        public string FormatProductNotification(WhatsAppProductNotification notification)
        {
            var message = new StringBuilder();

            // Add header with emoji based on notification type
            var emoji = notification.NotificationType switch
            {
                "created" => "✅",
                "transferred" => "🔄",
                _ => "📌"
            };

            message.AppendLine($"{emoji} *Product {notification.NotificationType.ToUpper()}*");
            message.AppendLine();

            // Add product details
            message.AppendLine($"📦 *Product Details:*");
            message.AppendLine($"• *Inventory Code:* {notification.InventoryCode}");
            message.AppendLine($"• *Vendor:* {notification.Vendor}");
            message.AppendLine($"• *Model:* {notification.Model}");
            if (notification.NotificationType == "created")
            {
                message.AppendLine($"• *Department:* {notification.ToDepartmentName}");

                if (!string.IsNullOrEmpty(notification.ToWorker))
                {
                    message.AppendLine($"• *Assigned Worker:* {notification.ToWorker}");
                }

                if (notification.IsNewItem)
                {
                    message.AppendLine($"• *Status:* 🆕 New Item");
                }
                if(!notification.IsWorking)
                {
                    message.AppendLine($"• *Status:* ❌ Not Working");
                }
            }
            else if(notification.NotificationType =="transferred")
            {
                message.AppendLine($"• *From Department:* {notification.FromDepartmentName}");

                if (!string.IsNullOrEmpty(notification.FromWorker))
                {
                    message.AppendLine($"• *From Worker:* {notification.FromWorker}");
                }

                message.AppendLine($"• *To Department:* {notification.ToDepartmentName}");

                if (!string.IsNullOrEmpty(notification.ToWorker))
                {
                    message.AppendLine($"• *Assigned Worker:* {notification.ToWorker}");
                }
            }

            if (!string.IsNullOrEmpty(notification.Notes))
            {
                message.AppendLine($"• *Notes:* {notification.Notes}");
            }

            message.AppendLine();
            message.AppendLine($"⏰ *Time:* {notification.CreatedAt:dd/MM/yyyy HH:mm}");

            // Add footer
            message.AppendLine();
            message.AppendLine("_This is an automated notification from 166 Logistics Inventory System_");

            return message.ToString();
        }


        private async Task<HttpResponseMessage> SendRequestAsync(string endpoint, object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogDebug($"Sending request to: {endpoint}");
            _logger.LogDebug($"Payload: {json}");

            return await _httpClient.PostAsync(endpoint, content);
        }
    }
}