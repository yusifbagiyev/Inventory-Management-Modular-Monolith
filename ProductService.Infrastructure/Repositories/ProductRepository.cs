using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ProductService.Domain.Common;
using ProductService.Domain.Entities;
using ProductService.Domain.Repositories;
using ProductService.Infrastructure.Data;
using SharedServices.Services;

namespace ProductService.Infrastructure.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly ProductDbContext _context;

        public ProductRepository(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Department)
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        }


        public async Task<PagedResult<Product>> GetAllAsync(
            int pageNumber,
            int pageSize,
            string? search,
            DateTime? startDate,
            DateTime? endDate,
            bool? status,
            bool? availability,
            int? categoryId = null,
            int? departmentId = null,
            bool? hasImage = null,
            bool? assigned = null,
            ProductListFilter? filter = null,
            CancellationToken cancellationToken = default)
        {
            // Clamp against a negative Skip but leave the size uncapped, since exports ask for more on purpose
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Max(1, pageSize);
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Department)
                .AsQueryable();

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId.Value);

            if (departmentId.HasValue)
                query = query.Where(p => p.DepartmentId == departmentId.Value);

            if (status.HasValue)
                query = query.Where(p => p.IsWorking == status.Value);

            if (availability.HasValue)
                query = query.Where(p => p.IsActive == availability.Value);

            // A missing image or worker is stored as NULL or as an empty string, so both count as missing
            if (hasImage.HasValue)
            {
                query = hasImage.Value
                    ? query.Where(p => p.ImageUrl != null && p.ImageUrl != "")
                    : query.Where(p => p.ImageUrl == null || p.ImageUrl == "");
            }

            if (assigned.HasValue)
            {
                query = assigned.Value
                    ? query.Where(p => p.Worker != null && p.Worker != "")
                    : query.Where(p => p.Worker == null || p.Worker == "");
            }

            if (startDate.HasValue)
            {
                query = query.Where(r => r.CreatedAt >= startDate);
            }

            // The end date counts as the whole day
            if (endDate.HasValue)
            {
                var EndDate = endDate.Value.AddDays(1).AddTicks(-1);
                query = query.Where(r => r.CreatedAt <= EndDate);
            }

            query = ApplyColumnFilters(query, filter);

            // Each word must match some field, since the words of a phrase often sit in different columns
            var terms = (search ?? "")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct()
                .ToArray();

            IEnumerable<Product> items;
            int totalCount;

            if (terms.Length > 0 || filter?.HasText == true)
            {
                // ILIKE doesn't fold Azerbaijani letters, so the database only narrows the rows down
                var broadQuery = query;
                foreach (var term in terms)
                {
                    var t = term;
                    broadQuery = broadQuery.Where(r =>
                        EF.Functions.ILike(r.InventoryCode.ToString(), $"%{t}%") ||
                        EF.Functions.ILike(r.Vendor, $"%{t}%") ||
                        EF.Functions.ILike(r.Model, $"%{t}%") ||
                        (r.Category != null && EF.Functions.ILike(r.Category.Name, $"%{t}%")) ||
                        (r.Department != null && EF.Functions.ILike(r.Department.Name, $"%{t}%")) ||
                        EF.Functions.ILike(r.Description ?? "", $"%{t}%") ||
                        EF.Functions.ILike(r.Worker ?? "", $"%{t}%")
                    );
                }

                var allFilteredItems = await Order(broadQuery, filter, NewestFirst).ToListAsync(cancellationToken);

                // Then match every word in memory with Azerbaijani folding
                items = allFilteredItems.Where(r => MatchesText(r, filter) && terms.All(t =>
                    SearchHelper.ContainsAzerbaijani(r.InventoryCode.ToString(), t) ||
                    SearchHelper.ContainsAzerbaijani(r.Vendor, t) ||
                    SearchHelper.ContainsAzerbaijani(r.Model, t) ||
                    SearchHelper.ContainsAzerbaijani(r.Category?.Name, t) ||
                    SearchHelper.ContainsAzerbaijani(r.Department?.Name, t) ||
                    SearchHelper.ContainsAzerbaijani(r.Description, t) ||
                    SearchHelper.ContainsAzerbaijani(r.Worker, t)
                )).ToList();

                totalCount = items.Count();

                items = items
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();
            }
            else
            {
                totalCount = await query.CountAsync(cancellationToken);

                items = await Order(query, filter, NewestFirst)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);
            }

            return new PagedResult<Product>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        private static IOrderedQueryable<Product> NewestFirst(IQueryable<Product> query)
            => query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.UpdatedAt);

        private static IOrderedQueryable<Product> LastDeletedFirst(IQueryable<Product> query)
            => query.OrderByDescending(p => p.DeletedAt);

        // The column header filters, where text filters only narrow the rows here and MatchesText decides
        private static IQueryable<Product> ApplyColumnFilters(IQueryable<Product> query, ProductListFilter? filter)
        {
            if (filter == null)
                return query;

            if (filter.CategoryIds is { Length: > 0 } categoryIds)
                query = query.Where(p => categoryIds.Contains(p.CategoryId));

            if (filter.DepartmentIds is { Length: > 0 } departmentIds)
                query = query.Where(p => departmentIds.Contains(p.DepartmentId));

            if (filter.UpdatedFrom is { } updatedFrom)
            {
                var from = updatedFrom.Date;
                query = query.Where(p => (p.UpdatedAt ?? p.CreatedAt) >= from);
            }

            if (filter.UpdatedTo is { } updatedTo)
            {
                var before = updatedTo.Date.AddDays(1);
                query = query.Where(p => (p.UpdatedAt ?? p.CreatedAt) < before);
            }

            if (filter.DeletedFrom is { } deletedFrom)
            {
                var from = deletedFrom.Date;
                query = query.Where(p => p.DeletedAt >= from);
            }

            if (filter.DeletedTo is { } deletedTo)
            {
                var before = deletedTo.Date.AddDays(1);
                query = query.Where(p => p.DeletedAt < before);
            }

            if (filter.DeletedBy is { Length: > 0 } deletedBy)
                query = query.Where(p => p.DeletedBy != null && deletedBy.Contains(p.DeletedBy));

            if (!string.IsNullOrWhiteSpace(filter.Code))
            {
                var code = filter.Code.Trim();
                query = query.Where(p => EF.Functions.ILike(p.InventoryCode.ToString(), $"%{code}%"));
            }

            if (!string.IsNullOrWhiteSpace(filter.Product))
            {
                var product = filter.Product.Trim();
                query = query.Where(p => EF.Functions.ILike(p.Model, $"%{product}%") || EF.Functions.ILike(p.Vendor, $"%{product}%"));
            }

            if (!string.IsNullOrWhiteSpace(filter.Worker))
            {
                var worker = filter.Worker.Trim();
                query = query.Where(p => EF.Functions.ILike(p.Worker ?? "", $"%{worker}%"));
            }

            return query;
        }

        // The text filters again with Azerbaijani folding, the way the search matches
        private static bool MatchesText(Product p, ProductListFilter? filter)
            => filter == null
               || ((string.IsNullOrWhiteSpace(filter.Code) || SearchHelper.ContainsAzerbaijani(p.InventoryCode.ToString(), filter.Code.Trim()))
                   && (string.IsNullOrWhiteSpace(filter.Product)
                       || SearchHelper.ContainsAzerbaijani(p.Model, filter.Product.Trim())
                       || SearchHelper.ContainsAzerbaijani(p.Vendor, filter.Product.Trim()))
                   && (string.IsNullOrWhiteSpace(filter.Worker) || SearchHelper.ContainsAzerbaijani(p.Worker, filter.Worker.Trim())));

        // The id breaks ties, so paging through equal values never repeats or skips a row
        private static IOrderedQueryable<Product> Order(
            IQueryable<Product> query, ProductListFilter? filter, Func<IQueryable<Product>, IOrderedQueryable<Product>> listOrder)
        {
            var desc = filter?.Descending == true;
            var ordered = filter?.Sort switch
            {
                "code" => By(query, p => p.InventoryCode, desc),
                "product" => Then(By(query, p => p.Model, desc), p => p.Vendor, desc),
                "category" => By(query, p => p.Category!.Name, desc),
                "department" => By(query, p => p.Department!.Name, desc),
                "worker" => By(query, p => p.Worker ?? "", desc),
                "updated" => By(query, p => p.UpdatedAt ?? p.CreatedAt, desc),
                "state" => Then(By(query, p => p.IsWorking, desc), p => p.IsActive, desc),
                "deleted" => By(query, p => p.DeletedAt, desc),
                "deletedBy" => By(query, p => p.DeletedBy ?? "", desc),
                _ => null
            };
            return ordered == null
                ? listOrder(query).ThenByDescending(p => p.Id)
                : Then(ordered, p => p.Id, desc);
        }

        private static IOrderedQueryable<Product> By<TKey>(IQueryable<Product> query, Expression<Func<Product, TKey>> key, bool descending)
            => descending ? query.OrderByDescending(key) : query.OrderBy(key);

        private static IOrderedQueryable<Product> Then<TKey>(IOrderedQueryable<Product> query, Expression<Func<Product, TKey>> key, bool descending)
            => descending ? query.ThenByDescending(key) : query.ThenBy(key);


        public async Task<IReadOnlyList<(int DepartmentId, int CategoryId)>> GetDepartmentCategoryPairsAsync(
            bool? status = null,
            bool? availability = null,
            bool? hasImage = null,
            bool? assigned = null,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Products.AsNoTracking().AsQueryable();

            // GetAllAsync's predicates minus department and category, so the dropdowns follow the other filters
            if (status.HasValue)
                query = query.Where(p => p.IsWorking == status.Value);

            if (availability.HasValue)
                query = query.Where(p => p.IsActive == availability.Value);

            if (hasImage.HasValue)
            {
                query = hasImage.Value
                    ? query.Where(p => p.ImageUrl != null && p.ImageUrl != "")
                    : query.Where(p => p.ImageUrl == null || p.ImageUrl == "");
            }

            if (assigned.HasValue)
            {
                query = assigned.Value
                    ? query.Where(p => p.Worker != null && p.Worker != "")
                    : query.Where(p => p.Worker == null || p.Worker == "");
            }

            // Anonymous type first, because EF can't translate a ValueTuple projection
            var pairs = await query
                .Select(p => new { p.DepartmentId, p.CategoryId })
                .Distinct()
                .ToListAsync(cancellationToken);

            return pairs.Select(x => (x.DepartmentId, x.CategoryId)).ToList();
        }


        public async Task<IEnumerable<Product>> GetByCategoryIdAsync(int categoryId, CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Department)
                .Where(p => p.CategoryId == categoryId)
                .ToListAsync(cancellationToken);
        }


        public async Task<IEnumerable<Product>> GetByDepartmentIdAsync(int departmentId, CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Department)
                .Where(p => p.DepartmentId == departmentId)
                .ToListAsync(cancellationToken);
        }


        public async Task<Product?> GetByInventoryCodeAsync(int inventoryCode, CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Department)
                .FirstOrDefaultAsync(p => p.InventoryCode == inventoryCode, cancellationToken);
        }


        public async Task<Product> AddAsync(Product product, CancellationToken cancellationToken = default)
        {
            await _context.Products.AddAsync(product, cancellationToken);
            return product;
        }


        public Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
        {
            _context.Entry(product).State = EntityState.Modified;
            return Task.CompletedTask;
        }


        public Task DeleteAsync(Product product, CancellationToken cancellationToken = default)
        {
            _context.Products.Remove(product);
            return Task.CompletedTask;
        }


        public async Task<bool> ExistsByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Products.AnyAsync(p => p.Id == id, cancellationToken);
        }


        public async Task<(int Total, int Active, int NotWorking)> CountCreatedAsync(DateTime? createdFrom, DateTime? createdTo, CancellationToken cancellationToken = default)
        {
            var query = _context.Products.AsNoTracking();
            if (createdFrom.HasValue) query = query.Where(p => p.CreatedAt >= createdFrom.Value);
            if (createdTo.HasValue) query = query.Where(p => p.CreatedAt <= createdTo.Value);

            // One round trip, translated to COUNT with FILTER clauses
            var counts = await query
                .GroupBy(_ => 1)
                .Select(g => new { Total = g.Count(), Active = g.Count(p => p.IsActive), NotWorking = g.Count(p => !p.IsWorking) })
                .FirstOrDefaultAsync(cancellationToken);
            return counts is null ? (0, 0, 0) : (counts.Total, counts.Active, counts.NotWorking);
        }

        public Task<int> CountAsync(CancellationToken cancellationToken = default)
            => _context.Products.CountAsync(cancellationToken);

        public async Task<int> CountByDepartmentIdAsync(int departmentId, CancellationToken cancellationToken = default)
        {
            return await _context.Products.CountAsync(p => p.DepartmentId == departmentId, cancellationToken);
        }


        public async Task<int> CountByCategoryIdAsync(int categoryId, CancellationToken cancellationToken = default)
        {
            return await _context.Products.CountAsync(p => p.CategoryId == categoryId, cancellationToken);
        }

        // The query filter hides deleted products, so these queries switch it off
        private IQueryable<Product> Deleted()
            => _context.Products.IgnoreQueryFilters().Where(p => p.IsDeleted);

        public async Task<(IReadOnlyList<Product> Items, int TotalCount)> GetDeletedAsync(
            string? search, int pageNumber, int pageSize, ProductListFilter? filter = null, CancellationToken cancellationToken = default)
        {
            var query = Deleted().AsNoTracking().Include(p => p.Category).Include(p => p.Department).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                foreach (var term in search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct())
                {
                    var t = term;
                    query = query.Where(p =>
                        EF.Functions.ILike(p.InventoryCode.ToString(), $"%{t}%") ||
                        EF.Functions.ILike(p.Model, $"%{t}%") ||
                        EF.Functions.ILike(p.Vendor, $"%{t}%") ||
                        EF.Functions.ILike(p.Worker ?? "", $"%{t}%") ||
                        EF.Functions.ILike(p.DeletedBy ?? "", $"%{t}%") ||
                        (p.Department != null && EF.Functions.ILike(p.Department.Name, $"%{t}%")));
                }
            }
            query = ApplyColumnFilters(query, filter);
            var skip = (Math.Max(1, pageNumber) - 1) * pageSize;

            if (filter?.HasText == true)
            {
                var matching = (await Order(query, filter, LastDeletedFirst).ToListAsync(cancellationToken))
                    .Where(p => MatchesText(p, filter))
                    .ToList();
                return (matching.Skip(skip).Take(pageSize).ToList(), matching.Count);
            }

            var total = await query.CountAsync(cancellationToken);
            var items = await Order(query, filter, LastDeletedFirst)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
            return (items, total);
        }

        public async Task<IReadOnlyList<string>> GetDeletedByNamesAsync(CancellationToken cancellationToken = default)
            => await Deleted()
                .Where(p => p.DeletedBy != null && p.DeletedBy != "")
                .Select(p => p.DeletedBy!)
                .Distinct()
                .OrderBy(name => name)
                .ToListAsync(cancellationToken);

        public Task<Product?> GetDeletedByIdAsync(int id, CancellationToken cancellationToken = default)
            => Deleted().AsNoTracking().Include(p => p.Category).Include(p => p.Department)
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        public Task<int> CountDeletedByDepartmentIdAsync(int departmentId, CancellationToken cancellationToken = default)
            => Deleted().CountAsync(p => p.DepartmentId == departmentId, cancellationToken);

        public Task<int> CountDeletedByCategoryIdAsync(int categoryId, CancellationToken cancellationToken = default)
            => Deleted().CountAsync(p => p.CategoryId == categoryId, cancellationToken);
    }
}