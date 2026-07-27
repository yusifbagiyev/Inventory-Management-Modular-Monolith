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


        public async Task<IEnumerable<InventoryRoute>> GetByDepartmentIdAsync(int departmentId, CancellationToken cancellationToken = default)
        {
            return await _context.InventoryRoutes
                .Where(r => r.FromDepartmentId == departmentId || r.ToDepartmentId == departmentId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync(cancellationToken);
        }


        public async Task<IReadOnlyList<(int DepartmentId, string CategoryName)>> GetDepartmentCategoryPairsAsync(
            bool? isCompleted = null,
            RouteType? routeType = null,
            CancellationToken cancellationToken = default)
        {
            var scoped = _context.InventoryRoutes.AsNoTracking().AsQueryable();

            // Apply the same status/type predicates the list uses, so the department and category
            // options reflect only what the other active filters allow.
            if (isCompleted.HasValue)
                scoped = scoped.Where(r => r.IsCompleted == isCompleted.Value);

            if (routeType.HasValue)
                scoped = scoped.Where(r => r.RouteType == routeType.Value);

            // Pull the distinct (from, to, category) triples, then fan each out to one pair per real
            // department end. Done in memory because a route has two department columns and dept 0
            // ("Removed") must be dropped - awkward to express as a single translatable query.
            var rows = await scoped
                .Select(r => new
                {
                    r.FromDepartmentId,
                    r.ToDepartmentId,
                    r.ProductSnapshot.CategoryName
                })
                .Distinct()
                .ToListAsync(cancellationToken);

            var pairs = new HashSet<(int, string)>();
            foreach (var row in rows)
            {
                if (string.IsNullOrEmpty(row.CategoryName))
                    continue;
                if (row.ToDepartmentId != 0)
                    pairs.Add((row.ToDepartmentId, row.CategoryName));
                if (row.FromDepartmentId.HasValue && row.FromDepartmentId.Value != 0)
                    pairs.Add((row.FromDepartmentId.Value, row.CategoryName));
            }

            return pairs.ToList();
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
            string? search,
            bool? isCompleted,
            DateTime? startDate,
            DateTime? endDate,
            int? departmentId = null,
            string? categoryName = null,
            RouteType? routeType = null,
            CancellationToken cancellationToken = default)
        {
            var query = _context.InventoryRoutes.AsQueryable();

            if (isCompleted.HasValue)
                query = query.Where(r => r.IsCompleted == isCompleted.Value);

            if (routeType.HasValue)
                query = query.Where(r => r.RouteType == routeType.Value);

            // A route touches two departments, so "in this department" means either end - same rule
            // as GetByDepartmentIdAsync.
            if (departmentId.HasValue)
                query = query.Where(r => r.FromDepartmentId == departmentId.Value || r.ToDepartmentId == departmentId.Value);

            // Category lives on the product snapshot as a name (there is no id to match on).
            if (!string.IsNullOrEmpty(categoryName))
                query = query.Where(r => r.ProductSnapshot.CategoryName == categoryName);

            if (startDate.HasValue)
            {
                query=query.Where(r=>r.CreatedAt>= startDate);
            }

            if (endDate.HasValue)
            {
                var EndDate = endDate.Value.AddDays(1).AddTicks(-1);
                query = query.Where(r => r.CreatedAt <= EndDate);
            }

            IEnumerable<InventoryRoute> items;
            int totalCount;

            if (!string.IsNullOrEmpty(search))
            {
                // Multi-word search: split into words and require EVERY word to appear in SOME
                // field (AND across words, OR across fields). Previously the whole phrase was one
                // ILIKE, so "abdulqadir abdullayev hp" matched nothing - no single field holds that
                // exact string, even though the worker is "Abdulqadir Abdullayev" and the vendor "HP".
                var tokens = search.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

                // Broad database prefilter, one AND-ed clause per word.
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
                    .ToListAsync(cancellationToken);

                // Azerbaijani-aware refine in memory: every word must match at least one field.
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