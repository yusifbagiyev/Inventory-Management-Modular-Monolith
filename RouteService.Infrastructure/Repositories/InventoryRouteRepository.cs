using Microsoft.EntityFrameworkCore;
using RouteService.Domain.Common;
using RouteService.Domain.Entities;
using RouteService.Domain.Enums;
using RouteService.Domain.Repositories;
using RouteService.Infrastructure.Data;

namespace RouteService.Infrastructure.Repositories
{
    public class InventoryRouteRepository : IInventoryRouteRepository
    {
        private readonly RouteDbContext _context;

        public InventoryRouteRepository(RouteDbContext context)
        {
            _context = context;
        }

        public async Task<InventoryRoute?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.InventoryRoutes
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        }

        public async Task<IEnumerable<InventoryRoute>> GetByProductIdAsync(int productId, CancellationToken cancellationToken = default)
        {
            return await _context.InventoryRoutes
                .Where(r => r.ProductSnapshot.ProductId == productId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<InventoryRoute>> GetByDepartmentIdAsync(int departmentId, CancellationToken cancellationToken = default)
        {
            return await _context.InventoryRoutes
                .Where(r => r.FromDepartmentId == departmentId || r.ToDepartmentId == departmentId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<InventoryRoute>> GetByRouteTypeAsync(RouteType routeType, CancellationToken cancellationToken = default)
        {
            return await _context.InventoryRoutes
                .Where(r => r.RouteType == routeType)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<InventoryRoute> AddAsync(InventoryRoute route, CancellationToken cancellationToken = default)
        {
            await _context.InventoryRoutes.AddAsync(route, cancellationToken);
            return route;
        }

        public Task UpdateAsync(InventoryRoute route, CancellationToken cancellationToken = default)
        {
            _context.Entry(route).State = EntityState.Modified;
            return Task.CompletedTask;
        }

        public async Task<InventoryRoute?> GetLatestRouteForProductAsync(int productId, CancellationToken cancellationToken = default)
        {
            return await _context.InventoryRoutes
                .Where(r => r.ProductSnapshot.ProductId == productId)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }
        public async Task<PagedResult<InventoryRoute>> GetAllAsync(
            int pageNumber,
            int pageSize,
            bool? isCompleted,
            DateTime? startDate,
            DateTime? endDate,
            CancellationToken cancellationToken = default)
        {
            var query = _context.InventoryRoutes.AsQueryable();

            if (isCompleted.HasValue)
                query = query.Where(r => r.IsCompleted == isCompleted.Value);

            if (startDate.HasValue)
            {
                var utcStartDate = DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc);
                query=query.Where(r=>r.CreatedAt>=utcStartDate);
            }

            if (endDate.HasValue)
            {
                var utcEndDate = DateTime.SpecifyKind(endDate.Value.AddDays(1).AddSeconds(-1), DateTimeKind.Utc);
                query = query.Where(r => r.CreatedAt <= utcEndDate);
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<InventoryRoute>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<IEnumerable<InventoryRoute>> GetIncompleteRoutesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.InventoryRoutes
                .Where(r => !r.IsCompleted)
                .OrderBy(r => r.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public Task DeleteAsync(InventoryRoute route, CancellationToken cancellationToken = default)
        {
            _context.InventoryRoutes.Remove(route);
            return Task.CompletedTask;
        }

        public async Task<InventoryRoute?> GetPreviousRouteForProductAsync(int productId, int currentRouteId, CancellationToken cancellationToken = default)
        {
            return await _context.InventoryRoutes
                .Where(r => r.ProductSnapshot.ProductId == productId && r.Id < currentRouteId)
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}