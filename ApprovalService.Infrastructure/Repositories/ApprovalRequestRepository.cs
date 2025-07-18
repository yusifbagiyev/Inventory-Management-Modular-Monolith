using ApprovalService.Domain.Entities;
using ApprovalService.Domain.Enums;
using ApprovalService.Domain.Repositories;
using ApprovalService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApprovalService.Infrastructure.Repositories
{
    public class ApprovalRequestRepository : IApprovalRequestRepository
    {
        private readonly ApprovalDbContext _context;

        public ApprovalRequestRepository(ApprovalDbContext context)
        {
            _context = context;
        }

        public async Task<ApprovalRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.ApprovalRequests.FindAsync(new object[] { id }, cancellationToken);
        }


        public async Task<IEnumerable<ApprovalRequest>> GetPendingAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            return await _context.ApprovalRequests
                .Where(r => r.Status == ApprovalStatus.Pending)
                .OrderByDescending(r => r.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }


        public async Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
        {
            return await _context.ApprovalRequests
                .CountAsync(r => r.Status == ApprovalStatus.Pending, cancellationToken);
        }


        public async Task<IEnumerable<ApprovalRequest>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _context.ApprovalRequests
                .Where(r => r.RequestedById == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync(cancellationToken);
        }


        public async Task<IEnumerable<ApprovalRequest>> GetByStatusAsync(ApprovalStatus status, CancellationToken cancellationToken = default)
        {
            return await _context.ApprovalRequests
                .Where(r => r.Status == status)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync(cancellationToken);
        }


        public async Task<ApprovalRequest> AddAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
        {
            await _context.ApprovalRequests.AddAsync(request, cancellationToken);
            return request;
        }


        public Task UpdateAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
        {
            _context.Entry(request).State = EntityState.Modified;
            return Task.CompletedTask;
        }

        public async Task<IEnumerable<ApprovalRequest>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.ApprovalRequests
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public Task DeleteAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
        {
            _context.ApprovalRequests.Remove(request);
            return Task.CompletedTask;
        }
    }
}