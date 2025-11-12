using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using System.Text;
using System.Web;

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


                var queryParams = new Dictionary<string, string>
                {
                    ["token"] = _settings.ApiToken,
                    ["to"] = groupId,
                    ["body"] = message,
                    ["priority"] = "10"
                };

                // Construct the API endpoint URL
                var endpoint = $"/{_settings.InstanceId}/messages/chat";

                var queryString = string.Join("&", queryParams.Select(kvp =>
                    $"{kvp.Key}={HttpUtility.UrlEncode(kvp.Value)}"));

                var fullEndpoint = $"{endpoint}?{queryString}";

                _logger.LogDebug($"Sending message to: {_httpClient.BaseAddress}{fullEndpoint}");

                // Send the HTTP request (UltraMsg uses GET for messages/chat)
                var response = await _httpClient.GetAsync(fullEndpoint);

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

        public async Task<bool> SendGroupMessageWithImageDataAsync(string groupId, string message, byte[] imageData, string fileName)
        {
            try
            {
                // Ensure group Id is in the correct format
                if (!groupId.EndsWith("@g.us"))
                    groupId = $"{groupId}@g.us";

                // Truncate caption if too long
                if (message.Length > 2048)
                {
                    _logger.LogWarning("Caption exceeds 2048 characters. Truncating...");
                    message = message.Substring(0, 2045) + "...";
                }

                // Check image size before attempting to send
                var imageSizeInMB = imageData.Length / (1024.0 * 1024.0);
                _logger.LogInformation($"Image size: {imageSizeInMB:F2} MB for file: {fileName}");

                if (imageSizeInMB > 10) // UltraMsg typically has a 10MB limit
                {
                    _logger.LogWarning($"Image size {imageSizeInMB:F2}MB exceeds limit. Sending text only.");
                    return await SendGroupMessageAsync(groupId, message);
                }

                // Convert image to base64 for UltraMsg API
                var base64Image = Convert.ToBase64String(imageData);
                var mimeType = GetMimeType(fileName);

                // For UltraMsg image endpoint
                var formData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("token", _settings.ApiToken),
                    new KeyValuePair<string, string>("to", groupId),
                    new KeyValuePair<string, string>("image", $"data:{mimeType};base64,{base64Image}"),
                    new KeyValuePair<string, string>("caption", message)
                });


                var endpoint = $"/{_settings.InstanceId}/messages/image";

                _logger.LogDebug($"Sending image to: {_httpClient.BaseAddress}{endpoint}");
                _logger.LogDebug($"Image size: {imageData.Length} bytes, MIME: {mimeType}");

                var response = await _httpClient.PostAsync(endpoint, formData);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"WhatsApp image sent to group {groupId}");
                    return true;
                }

                _logger.LogError($"Failed to upload file. Status: {response.StatusCode}, Error: {responseContent}");
                // Fallback to text message if image sending fails
                return await SendGroupMessageAsync(groupId, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending image to group {groupId}");
                // Fallback to text message if image sending fails
                return await SendGroupMessageAsync(groupId, message);
            }
        }

        private string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName)?.ToLowerInvariant() ?? "";
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };
        }

        public string FormatNotification(WhatsAppProductNotification notification)
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
            message.AppendLine($"• *Category:* {notification.CategoryName}");
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
            return message.ToString();
        }
    }
}