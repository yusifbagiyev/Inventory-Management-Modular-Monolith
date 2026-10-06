using ApprovalService.Domain.Entities;
using ApprovalService.Domain.Enums;

namespace ApprovalService.Domain.Repositories
{
    public interface IApprovalRequestRepository
    {
        /// <summary>The whole request with its uploaded images, for executing or changing it.</summary>
        Task<ApprovalRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<ApprovalRequestSummary?> GetSummaryByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ApprovalRequestSummary>> GetPendingAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        /// <summary>The newest pending requests of these types.</summary>
        Task<IReadOnlyList<ApprovalRequestSummary>> GetPendingAsync(IReadOnlyCollection<string> requestTypes, int take, CancellationToken cancellationToken = default);
        Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default);
        /// <summary>The latest requests in these states, newest decision first.</summary>
        Task<IReadOnlyList<ApprovalRequestSummary>> GetDecidedAsync(IReadOnlyCollection<ApprovalStatus> statuses, int take, CancellationToken cancellationToken = default);
        /// <summary>One page of a user's requests, newest first.</summary>
        Task<IReadOnlyList<ApprovalRequestSummary>> GetByUserIdAsync(int userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        /// <summary>How many requests a user has in each status.</summary>
        Task<IReadOnlyDictionary<ApprovalStatus, int>> CountByStatusAsync(int userId, CancellationToken cancellationToken = default);
        /// <summary>Counts requests in a status, optionally only those processed since a date.</summary>
        Task<int> CountAsync(ApprovalStatus status, DateTime? processedSince = null, CancellationToken cancellationToken = default);
        Task<ApprovalRequest> AddAsync(ApprovalRequest request, CancellationToken cancellationToken = default);
        Task UpdateAsync(ApprovalRequest request, CancellationToken cancellationToken = default);
        /// <summary>One page of every request, newest first.</summary>
        Task<IReadOnlyList<ApprovalRequestSummary>> GetAllAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        Task<int> CountAllAsync(CancellationToken cancellationToken = default);
        Task DeleteAsync(ApprovalRequest request, CancellationToken cancellationToken = default);
        /// <summary>Removes the uploaded image bytes of a decided request, keeping the file names, and tells whether it held any.</summary>
        Task<bool> DropImageDataAsync(int id, CancellationToken cancellationToken = default);
        /// <summary>Ids above afterId of decided requests large enough to still hold image bytes, in id order.</summary>
        Task<IReadOnlyList<int>> GetLargeDecidedIdsAsync(int afterId, int take, CancellationToken cancellationToken = default);
    }
}
