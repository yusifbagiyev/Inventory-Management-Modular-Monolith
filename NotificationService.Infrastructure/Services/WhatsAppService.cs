using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NotificationService.Infrastructure.Services
{
    public class WhatsAppService : IWhatsAppService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WhatsAppService> _logger;
        private readonly WhatsAppSettings _settings;

        // Response models for parsing API responses
        private class UploadResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("publicUrl")]
            public string? PublicUrl { get; set; }  // WaSender returns 'publicUrl', not nested in 'data'

            [JsonPropertyName("message")]
            public string? Message { get; set; }

            // These might be returned as well, based on typical API patterns
            [JsonPropertyName("fileId")]
            public string? FileId { get; set; }

            [JsonPropertyName("expiresAt")]
            public DateTime? ExpiresAt { get; set; }
        }

        private class UploadData
        {
            [JsonPropertyName("url")]
            public string? Url { get; set; }

            [JsonPropertyName("fileId")]
            public string? FileId { get; set; }

            [JsonPropertyName("expiresAt")]
            public DateTime? ExpiresAt { get; set; }
        }

        public WhatsAppService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<WhatsAppService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            _settings = configuration.GetSection("WhatsApp").Get<WhatsAppSettings>()
                ?? throw new InvalidOperationException("WhatsApp settings not found in configuration");

            // Configure the HTTP client with base address and authorization
            _httpClient.BaseAddress = new Uri(_settings.ApiUrl);
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _settings.ApiToken);
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Sends a text-only message to a WhatsApp group
        /// </summary>
        public async Task<bool> SendGroupMessageAsync(string groupId, string message)
        {
            try
            {
                // Ensure group ID has the correct format for WhatsApp groups
                if (!groupId.EndsWith("@g.us"))
                    groupId = $"{groupId}@g.us";

                var requestPayload = new
                {
                    to = groupId,
                    text = message
                };

                var jsonContent = JsonSerializer.Serialize(requestPayload);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger.LogDebug($"Sending text message to WhatsApp group: {groupId}");

                var response = await _httpClient.PostAsync("send-message", httpContent);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"WhatsApp message sent successfully to group {groupId}");
                    return true;
                }

                _logger.LogError($"Failed to send WhatsApp message. Status: {response.StatusCode}, Response: {responseContent}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception while sending WhatsApp message to group {groupId}");
                return false;
            }
        }

        public async Task<bool> SendGroupMessageWithImageDataAsync(string groupId, string message, byte[] imageData, string fileName)
        {
            try
            {
                if (!groupId.EndsWith("@g.us"))
                    groupId = $"{groupId}@g.us";

                // Validate message length
                if (message.Length > 2048)
                {
                    _logger.LogWarning("Caption exceeds 2048 characters. Truncating to fit limit.");
                    message = message.Substring(0, 2045) + "...";
                }

                // Check against WaSender's 16MB limit for images
                var imageSizeInMB = imageData.Length / (1024.0 * 1024.0);
                _logger.LogInformation($"Processing image: {fileName} ({imageSizeInMB:F2} MB)");

                if (imageSizeInMB > 5)
                {
                    _logger.LogWarning($"Image size {imageSizeInMB:F2}MB exceeds WaSender's 16MB limit. Sending text only.");
                    return await SendGroupMessageAsync(groupId, message);
                }

                // Step 1: Upload the image to get a temporary URL
                var imageUrl = await UploadImageToWaSender(imageData, fileName);

                if (string.IsNullOrEmpty(imageUrl))
                {
                    _logger.LogWarning("Failed to get image URL from upload. Falling back to text-only message.");
                    return await SendGroupMessageAsync(groupId, message);
                }

                // Step 2: Send the message with the uploaded image
                _logger.LogInformation($"Successfully uploaded image. Now sending WhatsApp message with image URL: {imageUrl}");

                var requestPayload = new
                {
                    to = groupId,
                    text = message,  // This becomes the caption for the image
                    imageUrl = imageUrl
                };

                var jsonContent = JsonSerializer.Serialize(requestPayload);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger.LogDebug($"Sending image message to WhatsApp group: {groupId}");

                var response = await _httpClient.PostAsync("send-message", httpContent);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"WhatsApp image message sent successfully to group {groupId}");
                    return true;
                }

                _logger.LogError($"Failed to send WhatsApp image message. Status: {response.StatusCode}, Response: {responseContent}");

                // Fallback to text-only if image message fails
                _logger.LogInformation("Attempting fallback to text-only message");
                return await SendGroupMessageAsync(groupId, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception while sending image message to group {groupId}");
                return await SendGroupMessageAsync(groupId, message);
            }
        }

        private async Task<string?> UploadImageToWaSender(byte[] imageData, string fileName)
        {
            try
            {
                var mimeType = GetMimeType(fileName);

                // Convert to Base64 with data URL prefix (WaSender's recommended format)
                var base64String = Convert.ToBase64String(imageData);
                var dataUrl = $"data:{mimeType};base64,{base64String}";

                var uploadPayload = new
                {
                    base64 = dataUrl
                };

                var jsonContent = JsonSerializer.Serialize(uploadPayload);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger.LogDebug($"Uploading image to WaSender: {fileName} ({imageData.Length} bytes, MIME: {mimeType})");

                var response = await _httpClient.PostAsync("upload", httpContent);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Upload request failed. Status: {response.StatusCode}, Response: {responseContent}");
                    return null;
                }

                // Parse the response to extract the public URL
                try
                {
                    _logger.LogDebug($"Upload response received: {responseContent}");

                    var uploadResponse = JsonSerializer.Deserialize<UploadResponse>(responseContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    // Check if upload was successful and we have a URL
                    if (uploadResponse?.Success == true && !string.IsNullOrEmpty(uploadResponse.PublicUrl))
                    {
                        _logger.LogInformation($"Image uploaded successfully. Public URL: {uploadResponse.PublicUrl}");

                        // Log expiry if available
                        if (uploadResponse.ExpiresAt.HasValue)
                        {
                            _logger.LogDebug($"Image URL expires at: {uploadResponse.ExpiresAt}");
                        }

                        return uploadResponse.PublicUrl;  // Return the public URL for use in the message
                    }

                    // If success is false or URL is missing
                    _logger.LogError($"Upload failed. Success: {uploadResponse?.Success}, " +
                                    $"PublicUrl: {uploadResponse?.PublicUrl ?? "null"}, " +
                                    $"Message: {uploadResponse?.Message ?? "none"}");
                    return null;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, $"Failed to parse upload response JSON. Raw response: {responseContent}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during image upload to WaSender");
                return null;
            }
        }

        /// <summary>
        /// Determines MIME type based on file extension
        /// </summary>
        private string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName)?.ToLowerInvariant() ?? "";

            // Return appropriate MIME type based on file extension
            // These are the common image formats WhatsApp supports
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                ".svg" => "image/svg+xml",
                _ => "image/jpeg"  // Default to JPEG if unknown
            };
        }

        /// <summary>
        /// Formats a product notification into a WhatsApp-friendly message with formatting
        /// </summary>
        public string FormatNotification(WhatsAppProductNotification notification)
        {
            var message = new StringBuilder();

            // Choose emoji based on notification type for visual distinction
            var emoji = notification.NotificationType switch
            {
                "created" => "✅",
                "transferred" => "🔄",
                "deleted" => "❌",
                _ => "📌"
            };

            // Build the formatted message with WhatsApp markdown
            message.AppendLine($"{emoji} *Product {notification.NotificationType.ToUpper()}*");
            message.AppendLine();

            // Product details section
            message.AppendLine($"📦 *Product Details:*");
            message.AppendLine($"• *Inventory Code:* {notification.InventoryCode}");
            message.AppendLine($"• *Category:* {notification.CategoryName}");
            message.AppendLine($"• *Vendor:* {notification.Vendor}");
            message.AppendLine($"• *Model:* {notification.Model}");

            // Add type-specific information
            if (notification.NotificationType == "created")
            {
                message.AppendLine($"• *Department:* {notification.ToDepartmentName}");

                if (!string.IsNullOrEmpty(notification.ToWorker))
                    message.AppendLine($"• *Assigned Worker:* {notification.ToWorker}");

                // Add status indicators
                if (notification.IsNewItem)
                    message.AppendLine($"• *Status:* 🆕 New Item");

                if (!notification.IsWorking)
                    message.AppendLine($"• *Status:* ❌ Not Working");
            }
            else if (notification.NotificationType == "transferred")
            {
                // Show transfer details
                message.AppendLine($"• *From Department:* {notification.FromDepartmentName}");

                if (!string.IsNullOrEmpty(notification.FromWorker))
                    message.AppendLine($"• *From Worker:* {notification.FromWorker}");

                message.AppendLine($"• *To Department:* {notification.ToDepartmentName}");

                if (!string.IsNullOrEmpty(notification.ToWorker))
                    message.AppendLine($"• *Assigned Worker:* {notification.ToWorker}");
            }

            // Add notes if present
            if (!string.IsNullOrEmpty(notification.Notes))
                message.AppendLine($"• *Notes:* {notification.Notes}");

            // Add timestamp
            message.AppendLine();
            message.AppendLine($"⏰ *Time:* {notification.CreatedAt:dd/MM/yyyy HH:mm}");

            return message.ToString();
        }
    }
}