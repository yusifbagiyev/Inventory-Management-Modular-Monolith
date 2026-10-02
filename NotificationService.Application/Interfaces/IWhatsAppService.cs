using NotificationService.Application.DTOs;

namespace NotificationService.Application.Interfaces
{
    public interface IWhatsAppService
    {
        Task<bool> SendGroupMessageAsync(string groupId, string message);
        Task<bool> SendGroupMessageWithImageDataAsync(string groupId, string message, byte[] imageData, string fileName);
        string FormatNotification(WhatsAppProductNotification notification);

        /// <summary>One send attempt that reports why it failed.</summary>
        Task<WhatsAppSendResult> SendAsync(string groupId, string message, byte[]? imageData, string fileName,
            string? uploadedImageUrl = null, CancellationToken cancellationToken = default);
    }

    /// <summary>The outcome of one send attempt.</summary>
    /// <remarks>On a rate limit RetryAfter says how long to wait. UploadedImageUrl can be reused on a retry.</remarks>
    public sealed record WhatsAppSendResult(bool Success, bool RateLimited, TimeSpan? RetryAfter, string? Error, string? UploadedImageUrl);
}