using System.Linq.Expressions;
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
            string? departmentName = null,
            RouteListFilter? filter = null)
        {
            filter ??= new RouteListFilter();
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

            query = ApplyColumnFilters(query, filter);

            IEnumerable<InventoryRoute> items;
            int totalCount;

            var tokens = Words(search);
            var productWords = Words(filter.Product);

            if (tokens.Length > 0 || productWords.Length > 0)
            {
                // ILIKE doesn't fold Azerbaijani letters, so the database only does a rough first pass
                var broadQuery = query;

                // Each word must match some field, since the words of a phrase often sit in different columns
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

                // The product column filter matches the code or the model the same way
                foreach (var word in productWords)
                {
                    var t = word;
                    broadQuery = broadQuery.Where(r =>
                        EF.Functions.ILike(r.ProductSnapshot.InventoryCode.ToString(), $"%{t}%") ||
                        EF.Functions.ILike(r.ProductSnapshot.Model, $"%{t}%"));
                }

                var allFilteredItems = await Sort(broadQuery, filter).ToListAsync(cancellationToken);

                // Then match every word in memory with Azerbaijani folding
                items = allFilteredItems.Where(r =>
                {
                    var code = r.ProductSnapshot.InventoryCode.ToString();
                    var fields = new[]
                    {
                        code,
                        r.ProductSnapshot.CategoryName,
                        r.ProductSnapshot.Vendor,
                        r.ProductSnapshot.Model,
                        r.FromDepartmentName,
                        r.ToDepartmentName,
                        r.FromWorker,
                        r.ToWorker
                    };
                    return tokens.All(word => fields.Any(f => SearchHelper.ContainsAzerbaijani(f, word)))
                        && productWords.All(word => SearchHelper.ContainsAzerbaijani(code, word)
                                                    || SearchHelper.ContainsAzerbaijani(r.ProductSnapshot.Model, word));
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

                items = await Sort(query, filter)
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


        private static string[] Words(string? text)
            => string.IsNullOrWhiteSpace(text) ? [] : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        /// <summary>The list's column filters, matched against the names stored on the route like the other filters.</summary>
        private static IQueryable<InventoryRoute> ApplyColumnFilters(IQueryable<InventoryRoute> query, RouteListFilter filter)
        {
            if (filter.FromDepartments is { Length: > 0 } fromNames)
                query = query.Where(r => r.FromDepartmentName != null && fromNames.Contains(r.FromDepartmentName));

            if (filter.ToDepartments is { Length: > 0 } toNames)
                query = query.Where(r => toNames.Contains(r.ToDepartmentName));

            if (filter.Categories is { Length: > 0 } categories)
                query = query.Where(r => categories.Contains(r.ProductSnapshot.CategoryName));

            if (filter.RouteTypes is { Length: > 0 } types)
                query = query.Where(r => types.Contains(r.RouteType));

            if (filter.WhatsApp is { Length: > 0 } statuses)
            {
                // Routes that never sent a message have no status, which the filter calls None
                var orNone = statuses.Contains(RouteListFilter.NoWhatsApp);
                query = query.Where(r => r.WhatsAppStatus == null ? orNone : statuses.Contains(r.WhatsAppStatus));
            }

            return query;
        }

        /// <summary>Orders by the chosen column, newest first within equal values, with the id keeping pages stable.</summary>
        private static IQueryable<InventoryRoute> Sort(IQueryable<InventoryRoute> query, RouteListFilter filter)
        {
            var desc = filter.Descending;
            if (filter.Sort == "date")
            {
                return desc
                    ? query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
                    : query.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id);
            }

            var ordered = filter.Sort switch
            {
                "product" => Then(By(query, r => r.ProductSnapshot.Model, desc), r => r.ProductSnapshot.InventoryCode, desc),
                // Routes into the inventory have no sending end, and they stay last in both directions
                "from" => Then(Then(query.OrderBy(r => r.FromDepartmentName == null), r => r.FromDepartmentName, desc), r => r.FromWorker, desc),
                "to" => Then(By(query, r => r.ToDepartmentName, desc), r => r.ToWorker, desc),
                "category" => By(query, r => r.ProductSnapshot.CategoryName, desc),
                // The type is stored as its English name, so the order follows the filter's list instead of that spelling
                "type" => By(query, r => r.RouteType == RouteType.New ? 0
                    : r.RouteType == RouteType.Existing ? 1
                    : r.RouteType == RouteType.Update ? 2
                    : r.RouteType == RouteType.CodeChange ? 3
                    : r.RouteType == RouteType.Transfer ? 4 : 5, desc),
                "status" => By(query, r => r.IsCompleted, desc),
                "whatsapp" => Then(query.OrderBy(r => r.WhatsAppStatus == null), r => r.WhatsAppStatus, desc),
                _ => null
            };

            if (ordered == null)
            {
                return query
                    .OrderByDescending(r => !r.IsCompleted)
                    .ThenByDescending(r => r.CompletedAt)
                    // Pending routes share the same CompletedAt, so these keep paging stable
                    .ThenByDescending(r => r.CreatedAt)
                    .ThenByDescending(r => r.Id);
            }

            return ordered.ThenByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id);
        }

        private static IOrderedQueryable<InventoryRoute> By<TKey>(
            IQueryable<InventoryRoute> query, Expression<Func<InventoryRoute, TKey>> key, bool descending)
            => descending ? query.OrderByDescending(key) : query.OrderBy(key);

        private static IOrderedQueryable<InventoryRoute> Then<TKey>(
            IOrderedQueryable<InventoryRoute> query, Expression<Func<InventoryRoute, TKey>> key, bool descending)
            => descending ? query.ThenByDescending(key) : query.ThenBy(key);


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