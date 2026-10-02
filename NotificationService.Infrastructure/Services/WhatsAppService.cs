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
    /// <summary>WaSender client that posts group messages, uploading an image first to get a temporary URL for it.</summary>
    public class WhatsAppService : IWhatsAppService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WhatsAppService> _logger;
        private readonly WhatsAppSettings _settings;

        private class UploadResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("publicUrl")]
            public string? PublicUrl { get; set; }  // WaSender puts publicUrl at the top level, not inside data

            [JsonPropertyName("message")]
            public string? Message { get; set; }

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

            // Missing settings must not throw here, or the dispatcher would stop in-app notifications too
            _settings = configuration.GetSection("WhatsApp").Get<WhatsAppSettings>() ?? new WhatsAppSettings();

            if (Uri.TryCreate(_settings.ApiUrl, UriKind.Absolute, out var apiUrl))
                _httpClient.BaseAddress = apiUrl;
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _settings.ApiToken);
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>Sends a text-only message to a WhatsApp group.</summary>
        public async Task<bool> SendGroupMessageAsync(string groupId, string message)
        {
            try
            {
                // Group ids need the @g.us suffix
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

                if (message.Length > 2048)
                {
                    _logger.LogWarning("Caption exceeds 2048 characters. Truncating to fit limit.");
                    message = message.Substring(0, 2045) + "...";
                }

                // Images over 5 MB go as text only
                var imageSizeInMB = imageData.Length / (1024.0 * 1024.0);
                _logger.LogInformation($"Processing image: {fileName} ({imageSizeInMB:F2} MB)");

                if (imageSizeInMB > 5)
                {
                    _logger.LogWarning($"Image size {imageSizeInMB:F2}MB is over the 5 MB limit. Sending text only.");
                    return await SendGroupMessageAsync(groupId, message);
                }

                // The message needs a URL, so upload the image first to get a temporary one
                var imageUrl = await UploadImageToWaSender(imageData, fileName);

                if (string.IsNullOrEmpty(imageUrl))
                {
                    _logger.LogWarning("Failed to get image URL from upload. Falling back to text-only message.");
                    return await SendGroupMessageAsync(groupId, message);
                }

                _logger.LogInformation($"Successfully uploaded image. Now sending WhatsApp message with image URL: {imageUrl}");

                var requestPayload = new
                {
                    to = groupId,
                    text = message,  // Shown as the image caption
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

                _logger.LogInformation("Attempting fallback to text-only message");
                return await SendGroupMessageAsync(groupId, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception while sending image message to group {groupId}");
                return await SendGroupMessageAsync(groupId, message);
            }
        }

        /// <summary>One attempt to post a group message, reusing uploadedImageUrl on retries and reporting the wait a 429 asks for.</summary>
        public async Task<WhatsAppSendResult> SendAsync(string groupId, string message, byte[]? imageData, string fileName,
            string? uploadedImageUrl = null, CancellationToken cancellationToken = default)
        {
            if (!groupId.EndsWith("@g.us"))
                groupId = $"{groupId}@g.us";
            if (message.Length > 2048)
                message = message[..2045] + "...";

            var imageUrl = uploadedImageUrl;
            if (imageUrl == null && imageData is { Length: > 0 } && imageData.Length <= 5 * 1024 * 1024)
                imageUrl = await UploadImageToWaSender(imageData, fileName);   // On failure the text goes alone

            object payload = imageUrl == null ? new { to = groupId, text = message } : new { to = groupId, text = message, imageUrl };
            try
            {
                using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                using var response = await _httpClient.PostAsync("send-message", content, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("WhatsApp message sent to group {Group}", groupId);
                    return new WhatsAppSendResult(true, false, null, null, imageUrl);
                }

                var error = $"{(int)response.StatusCode} {response.StatusCode}: {(body.Length > 300 ? body[..300] : body)}";
                if ((int)response.StatusCode == 429)
                {
                    TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta;
                    try
                    {
                        using var json = JsonDocument.Parse(body);
                        if (json.RootElement.TryGetProperty("retry_after", out var seconds) && seconds.TryGetDouble(out var value))
                            retryAfter = TimeSpan.FromSeconds(value);
                    }
                    catch (JsonException) { }
                    _logger.LogWarning("WhatsApp rate limit, retrying after {Wait}: {Body}", retryAfter, body);
                    return new WhatsAppSendResult(false, true, retryAfter, error, imageUrl);
                }

                _logger.LogError("WhatsApp message failed: {Error}", error);
                return new WhatsAppSendResult(false, false, null, error, imageUrl);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogError(ex, "WhatsApp message to group {Group} failed", groupId);
                return new WhatsAppSendResult(false, false, null, ex.Message, imageUrl);
            }
        }

        private async Task<string?> UploadImageToWaSender(byte[] imageData, string fileName)
        {
            try
            {
                var mimeType = GetMimeType(fileName);

                // WaSender expects the image as a base64 data URL
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

                try
                {
                    _logger.LogDebug($"Upload response received: {responseContent}");

                    var uploadResponse = JsonSerializer.Deserialize<UploadResponse>(responseContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (uploadResponse?.Success == true && !string.IsNullOrEmpty(uploadResponse.PublicUrl))
                    {
                        _logger.LogInformation($"Image uploaded successfully. Public URL: {uploadResponse.PublicUrl}");

                        if (uploadResponse.ExpiresAt.HasValue)
                        {
                            _logger.LogDebug($"Image URL expires at: {uploadResponse.ExpiresAt}");
                        }

                        return uploadResponse.PublicUrl;
                    }

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

        private string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName)?.ToLowerInvariant() ?? "";

            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                ".svg" => "image/svg+xml",
                _ => "image/jpeg"
            };
        }

        /// <summary>Formats a product notification as a WhatsApp message.</summary>
        public string FormatNotification(WhatsAppProductNotification notification)
        {
            var message = new StringBuilder();

            var emoji = notification.NotificationType switch
            {
                "created" => "✅",
                "transferred" => "🔄",
                "deleted" => "❌",
                _ => "📌"
            };

            // Text between asterisks is bold in WhatsApp
            message.AppendLine($"{emoji} *Product {notification.NotificationType.ToUpper()}*");
            message.AppendLine();

            message.AppendLine($"📦 *Product Details:*");
            message.AppendLine($"• *Inventory Code:* {notification.InventoryCode}");
            message.AppendLine($"• *Category:* {notification.CategoryName}");
            message.AppendLine($"• *Vendor:* {notification.Vendor}");
            message.AppendLine($"• *Model:* {notification.Model}");

            if (notification.NotificationType == "created")
            {
                message.AppendLine($"• *Department:* {notification.ToDepartmentName}");

                if (!string.IsNullOrEmpty(notification.ToWorker))
                    message.AppendLine($"• *Assigned Worker:* {notification.ToWorker}");

                if (notification.IsNewItem)
                    message.AppendLine($"• *Status:* 🆕 New Item");

                if (!notification.IsWorking)
                    message.AppendLine($"• *Status:* ❌ Not Working");
            }
            else if (notification.NotificationType == "transferred")
            {
                message.AppendLine($"• *From Department:* {notification.FromDepartmentName}");

                if (!string.IsNullOrEmpty(notification.FromWorker))
                    message.AppendLine($"• *From Worker:* {notification.FromWorker}");

                message.AppendLine($"• *To Department:* {notification.ToDepartmentName}");

                if (!string.IsNullOrEmpty(notification.ToWorker))
                    message.AppendLine($"• *Assigned Worker:* {notification.ToWorker}");
            }

            if (!string.IsNullOrEmpty(notification.Notes))
                message.AppendLine($"• *Notes:* {notification.Notes}");

            message.AppendLine();
            message.AppendLine($"⏰ *Time:* {notification.CreatedAt:dd/MM/yyyy HH:mm}");

            return message.ToString();
        }
    }
}