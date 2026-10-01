using NotificationService.Application.DTOs;

namespace NotificationService.Application.Interfaces
{
    public interface IWhatsAppService
    {
        Task<bool> SendGroupMessageAsync(string groupId, string message);
        Task<bool> SendGroupMessageWithImageDataAsync(string groupId, string message, byte[] imageData, string fileName);
        string FormatNotification(WhatsAppProductNotification notification);

        /// <summary>One send attempt that says why it failed (see WhatsAppSendResult).</summary>
        Task<WhatsAppSendResult> SendAsync(string groupId, string message, byte[]? imageData, string fileName,
            string? uploadedImageUrl = null, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// The outcome of one send. RateLimited: the service refused for now and RetryAfter says how
    /// long to wait. UploadedImageUrl: the image's URL at the service, to reuse on a retry.
    /// </summary>
    public sealed record WhatsAppSendResult(bool Success, bool RateLimited, TimeSpan? RetryAfter, string? Error, string? UploadedImageUrl);
}