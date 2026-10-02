using Microsoft.EntityFrameworkCore;
using RouteService.Domain.Common;
using RouteService.Domain.Entities;
using RouteService.Domain.Enums;
using RouteService.Domain.Repositories;
using RouteService.Infrastructure.Data;
using SharedServices.Services;

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


        public async Task<IReadOnlyList<(string DepartmentName, string CategoryName)>> GetDepartmentCategoryPairsAsync(
            bool? isCompleted = null,
            RouteType? routeType = null,
            CancellationToken cancellationToken = default)
        {
            var scoped = _context.InventoryRoutes.AsNoTracking().AsQueryable();

            // Same status and type predicates as the list, so the options follow the other filters
            if (isCompleted.HasValue)
                scoped = scoped.Where(r => r.IsCompleted == isCompleted.Value);

            if (routeType.HasValue)
                scoped = scoped.Where(r => r.RouteType == routeType.Value);

            // One pair per real end of a route, where department 0 is a removal's placeholder
            var rows = await scoped
                .Select(r => new
                {
                    r.FromDepartmentId,
                    r.FromDepartmentName,
                    r.ToDepartmentId,
                    r.ToDepartmentName,
                    r.ProductSnapshot.CategoryName
                })
                .Distinct()
                .ToListAsync(cancellationToken);

            var pairs = new HashSet<(string, string)>();
            foreach (var row in rows)
            {
                if (string.IsNullOrEmpty(row.CategoryName))
                    continue;
                if (row.ToDepartmentId != 0 && !string.IsNullOrEmpty(row.ToDepartmentName))
                    pairs.Add((row.ToDepartmentName, row.CategoryName));
                if (row.FromDepartmentId is > 0 && !string.IsNullOrEmpty(row.FromDepartmentName))
                    pairs.Add((row.FromDepartmentName, row.CategoryName));
            }

            return pairs.ToList();
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


        public async Task<PagedResult<InventoryRoute>> GetAllAsync(
            int pageNumber,
            int pageSize,
            string? search,
            bool? isCompleted,
            DateTime? startDate,
            DateTime? endDate,
            int? departmentId = null,
            string? categoryName = null,
            RouteType? routeType = null,
            CancellationToken cancellationToken = default,
            string? departmentName = null)
        {
            // Clamp against a negative Skip but leave the size uncapped, since exports ask for more on purpose
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Max(1, pageSize);
            var query = _context.InventoryRoutes.AsNoTracking().AsQueryable();

            if (isCompleted.HasValue)
                query = query.Where(r => r.IsCompleted == isCompleted.Value);

            if (routeType.HasValue)
                query = query.Where(r => r.RouteType == routeType.Value);

            // A route touches two departments, so either end matches
            if (departmentId.HasValue)
                query = query.Where(r => r.FromDepartmentId == departmentId.Value || r.ToDepartmentId == departmentId.Value);

            // Match the name stored on the route, which is what the list shows for renamed or deleted departments
            if (!string.IsNullOrEmpty(departmentName))
                query = query.Where(r => r.FromDepartmentName == departmentName || r.ToDepartmentName == departmentName);

            // The snapshot stores only the category name, there is no id to match on
            if (!string.IsNullOrEmpty(categoryName))
                query = query.Where(r => r.ProductSnapshot.CategoryName == categoryName);

            if (startDate.HasValue)
            {
                query=query.Where(r=>r.CreatedAt>= startDate);
            }

            // The end date counts as the whole day
            if (endDate.HasValue)
            {
                var EndDate = endDate.Value.AddDays(1).AddTicks(-1);
                query = query.Where(r => r.CreatedAt <= EndDate);
            }

            IEnumerable<InventoryRoute> items;
            int totalCount;

            if (!string.IsNullOrEmpty(search))
            {
                // Each word must match some field, since the words of a phrase often sit in different columns
                var tokens = search.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

                // ILIKE doesn't fold Azerbaijani letters, so the database only does a rough first pass
                var broadQuery = query;
                foreach (var word in tokens)
                {
                    var t = word;
                    broadQuery = broadQuery.Where(r =>
                        EF.Functions.ILike(r.ProductSnapshot.InventoryCode.ToString(), $"%{t}%") ||
                        EF.Functions.ILike(r.ProductSnapshot.CategoryName, $"%{t}%") ||
                        EF.Functions.ILike(r.ProductSnapshot.Vendor, $"%{t}%") ||
                        EF.Functions.ILike(r.ProductSnapshot.Model, $"%{t}%") ||
                        (r.FromDepartmentName != null && EF.Functions.ILike(r.FromDepartmentName, $"%{t}%")) ||
                        EF.Functions.ILike(r.ToDepartmentName, $"%{t}%") ||
                        (r.FromWorker != null && EF.Functions.ILike(r.FromWorker, $"%{t}%")) ||
                        (r.ToWorker != null && EF.Functions.ILike(r.ToWorker, $"%{t}%"))
                    );
                }

                var allFilteredItems = await broadQuery
                    .OrderByDescending(r => !r.IsCompleted)
                    .ThenByDescending(r => r.CompletedAt)
                    // Pending routes share the same CompletedAt, so these keep paging stable
                    .ThenByDescending(r => r.CreatedAt)
                    .ThenByDescending(r => r.Id)
                    .ToListAsync(cancellationToken);

                // Then match every word in memory with Azerbaijani folding
                items = allFilteredItems.Where(r =>
                {
                    var fields = new[]
                    {
                        r.ProductSnapshot.InventoryCode.ToString(),
                        r.ProductSnapshot.CategoryName,
                        r.ProductSnapshot.Vendor,
                        r.ProductSnapshot.Model,
                        r.FromDepartmentName,
                        r.ToDepartmentName,
                        r.FromWorker,
                        r.ToWorker
                    };
                    return tokens.All(word => fields.Any(f => SearchHelper.ContainsAzerbaijani(f, word)));
                }).ToList();

                totalCount = items.Count();

                items = items
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();
            }
            else
            {
                totalCount = await query.CountAsync(cancellationToken);

                items = await query
                    .OrderByDescending(r => !r.IsCompleted)
                    .ThenByDescending(r => r.CompletedAt)
                    // Pending routes share the same CompletedAt, so these keep paging stable
                    .ThenByDescending(r => r.CreatedAt)
                    .ThenByDescending(r => r.Id)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);
            }

            return new PagedResult<InventoryRoute>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }


        public async Task<IReadOnlyList<TransferActivity>> GetTransferActivityAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
            => await _context.InventoryRoutes
                .AsNoTracking()
                .Where(r => r.RouteType == RouteType.Transfer && r.CreatedAt >= from && r.CreatedAt <= to)
                .Select(r => new TransferActivity(
                    r.ProductSnapshot.ProductId,
                    r.FromDepartmentId,
                    r.FromDepartmentName,
                    r.ToDepartmentId,
                    r.ToDepartmentName,
                    r.FromWorker,
                    r.ToWorker,
                    r.ProductSnapshot.CategoryName,
                    r.IsCompleted,
                    r.CreatedAt))
                .ToListAsync(cancellationToken);


        public Task<bool> HasPendingRouteForProductAsync(int productId, CancellationToken cancellationToken = default)
            => _context.InventoryRoutes.AnyAsync(
                r => r.ProductSnapshot.ProductId == productId && !r.IsCompleted && r.RouteType == RouteType.Transfer,
                cancellationToken);


        public Task DeleteAsync(InventoryRoute route, CancellationToken cancellationToken = default)
        {
            _context.InventoryRoutes.Remove(route);
            return Task.CompletedTask;
        }


    }
}