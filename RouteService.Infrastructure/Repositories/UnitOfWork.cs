using RouteService.Domain.Repositories;
using RouteService.Infrastructure.Data;

namespace RouteService.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly RouteDbContext _context;

        public UnitOfWork(RouteDbContext context)
        {
            _context = context;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => _context.SaveChangesAsync(cancellationToken);
    }
}
