using ApprovalService.Domain.Repositories;
using ApprovalService.Infrastructure.Data;

namespace ApprovalService.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApprovalDbContext _context;

        public UnitOfWork(ApprovalDbContext context)
        {
            _context = context;
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
    }
}