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
            CancellationToken cancellationToken = default)
        {
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

            // "No image" / "Unassigned" quick filters. A missing value is stored as either NULL or
            // an empty string, so both count as "no image" / "unassigned".
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

            if (endDate.HasValue)
            {
                var EndDate = endDate.Value.AddDays(1).AddTicks(-1);
                query = query.Where(r => r.CreatedAt <= EndDate);
            }

            IEnumerable<Product> items;
            int totalCount;

            if (!string.IsNullOrEmpty(search))
            {
                // Multi-word search: each whitespace-separated term must match somewhere on the
                // product (AND across terms, OR across fields). Matching the whole phrase as a
                // single string meant "tp link router" could never hit, because no single column
                // contains all of it - the vendor holds "Tp Link" and the category holds "Router".
                var terms = search
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct()
                    .ToArray();

                // First, apply a broad database filter to reduce the dataset
                // This uses standard SQL ILIKE which works but isn't perfect for Azerbaijani
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

                // Load the filtered results into memory
                var allFilteredItems = await broadQuery
                    .OrderByDescending(r => r.CreatedAt)
                    .ThenByDescending(r => r.UpdatedAt)
                    .ToListAsync(cancellationToken);

                // Now apply Azerbaijani-aware search in memory for precision - every term must hit
                items = allFilteredItems.Where(r => terms.All(t =>
                    SearchHelper.ContainsAzerbaijani(r.InventoryCode.ToString(), t) ||
                    SearchHelper.ContainsAzerbaijani(r.Vendor, t) ||
                    SearchHelper.ContainsAzerbaijani(r.Model, t) ||
                    SearchHelper.ContainsAzerbaijani(r.Category?.Name, t) ||
                    SearchHelper.ContainsAzerbaijani(r.Department?.Name, t) ||
                    SearchHelper.ContainsAzerbaijani(r.Description, t) ||
                    SearchHelper.ContainsAzerbaijani(r.Worker, t)
                )).ToList();

                totalCount = items.Count();

                // Apply pagination in memory
                items = items
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();
            }
            else
            {
                // No search term - use standard database pagination
                totalCount = await query.CountAsync(cancellationToken);

                items = await query
                    .OrderByDescending(r => r.CreatedAt)
                    .ThenByDescending(r => r.UpdatedAt)
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


        public async Task<IReadOnlyList<(int DepartmentId, int CategoryId)>> GetDepartmentCategoryPairsAsync(
            CancellationToken cancellationToken = default)
        {
            // One row per (department, category) that occurs at least once. Projected to an anonymous
            // type first because EF cannot translate a ValueTuple projection.
            var pairs = await _context.Products
                .AsNoTracking()
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


        public async Task<int> CountByDepartmentIdAsync(int departmentId, CancellationToken cancellationToken = default)
        {
            return await _context.Products.CountAsync(p => p.DepartmentId == departmentId, cancellationToken);
        }


        public async Task<int> CountByCategoryIdAsync(int categoryId, CancellationToken cancellationToken = default)
        {
            return await _context.Products.CountAsync(p => p.CategoryId == categoryId, cancellationToken);
        }
    }
}