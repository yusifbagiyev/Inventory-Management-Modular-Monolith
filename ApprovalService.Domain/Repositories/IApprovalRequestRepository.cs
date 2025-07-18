using ApprovalService.Domain.Entities;
using ApprovalService.Domain.Enums;

namespace ApprovalService.Domain.Repositories
{
    public interface IApprovalRequestRepository
    {
        Task<ApprovalRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<IEnumerable<ApprovalRequest>> GetPendingAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<ApprovalRequest>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<ApprovalRequest>> GetByStatusAsync(ApprovalStatus status, CancellationToken cancellationToken = default);
        Task<ApprovalRequest> AddAsync(ApprovalRequest request, CancellationToken cancellationToken = default);
        Task UpdateAsync(ApprovalRequest request, CancellationToken cancellationToken = default);
        Task<IEnumerable<ApprovalRequest>> GetAllAsync(CancellationToken cancellationToken = default);
        Task DeleteAsync(ApprovalRequest request, CancellationToken cancellationToken = default);
    }
}