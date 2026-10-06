using NotificationService.Application.DTOs;

namespace NotificationService.Application.Interfaces
{
    public interface IWhatsAppService
    {
        string FormatNotification(WhatsAppProductNotification notification);

        /// <summary>One send attempt that reports why it failed.</summary>
        Task<WhatsAppSendResult> SendAsync(string groupId, string message, byte[]? imageData, string fileName,
            string? uploadedImageUrl = null, CancellationToken cancellationToken = default);

        /// <summary>One attempt to delete a sent message for everyone.</summary>
        Task<WhatsAppDeleteResult> DeleteAsync(long messageId, CancellationToken cancellationToken = default);
    }

    /// <summary>One send attempt's outcome, with the wait asked by a rate limit, an uploaded image to reuse on a retry, whether the message may have gone out although no answer came, and the id of a sent one.</summary>
    public sealed record WhatsAppSendResult(bool Success, bool RateLimited, TimeSpan? RetryAfter, string? Error, string? UploadedImageUrl,
        bool OutcomeUnknown = false, long? MessageId = null);

    public sealed record WhatsAppDeleteResult(bool Success, bool RateLimited, string? Error);
}