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

    /// <summary>One send attempt's outcome, with the wait asked by a rate limit, an uploaded image to reuse on a retry, and whether the message may have gone out although no answer came.</summary>
    public sealed record WhatsAppSendResult(bool Success, bool RateLimited, TimeSpan? RetryAfter, string? Error, string? UploadedImageUrl,
        bool OutcomeUnknown = false);
}