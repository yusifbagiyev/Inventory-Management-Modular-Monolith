using SharedServices.Persistence;
using Microsoft.EntityFrameworkCore;
using ProductService.Domain.Common;
using ProductService.Domain.Entities;
using ProductService.Domain.Repositories;
using ProductService.Infrastructure.Data;
using SharedServices.Services;

namespace ProductService.Infrastructure.Repositories
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly ProductDbContext _context;

        public CategoryRepository(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        }

        public async Task<(int Active, int Inactive)> CountByActivityAsync(CancellationToken cancellationToken = default)
        {
            var active = await _context.Categories.CountAsync(x => x.IsActive, cancellationToken);
            var inactive = await _context.Categories.CountAsync(x => !x.IsActive, cancellationToken);
            return (active, inactive);
        }

        public Task<int> CountWithProductsAsync(CancellationToken cancellationToken = default)
            => _context.Products.Select(p => p.CategoryId).Distinct().CountAsync(cancellationToken);

        public async Task<IReadOnlyList<LookupItem>> GetLookupAsync(CancellationToken cancellationToken = default)
            => await _context.Categories
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new LookupItem(x.Id, x.Name, x.IsActive))
                .ToListAsync(cancellationToken);

        public async Task<IEnumerable<Category>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Categories
                .ToListAsync(cancellationToken);
        }


        public async Task<PagedResult<Category>> GetPagedAsync(
            int pageNumber, int pageSize, string? search, CancellationToken cancellationToken = default)
        {
            var query = _context.Categories.AsNoTracking().AsQueryable();

            IEnumerable<Category> items;
            int totalCount;

            if (!string.IsNullOrEmpty(search))
            {
                search = search.Trim();
                // The database match folds Azerbaijani letters, and the same rule runs again in memory
                var broadQuery = query.Where(r =>
                    EF.Functions.ILike(SearchSql.Fold(r.Name), SearchSql.Contains(search)) ||
                    EF.Functions.ILike(SearchSql.Fold(r.Description), SearchSql.Contains(search))
                );

                var allFilteredItems = await broadQuery
                    .OrderBy(n=>n.Name)
                    .ToListAsync(cancellationToken);

                items = allFilteredItems.Where(c =>
                    SearchHelper.ContainsAzerbaijani(c.Name, search) ||
                    SearchHelper.ContainsAzerbaijani(c.Description, search))
                    .ToList();

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
                    .OrderBy(c => c.Name)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);
            }
            return new PagedResult<Category>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }


        public async Task<Category> AddAsync(Category category, CancellationToken cancellationToken = default)
        {
            await _context.Categories.AddAsync(category, cancellationToken);
            return category;
        }

        public Task UpdateAsync(Category category, CancellationToken cancellationToken = default)
        {
            _context.Entry(category).State = EntityState.Modified;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Category category, CancellationToken cancellationToken = default)
        {
            _context.Categories.Remove(category);
            return Task.CompletedTask;
        }

        public async Task<Dictionary<int, int>> GetProductCountsAsync(IEnumerable<int> categoryIds, CancellationToken cancellationToken = default)
        {
            var ids = categoryIds.ToList();
            return await _context.Products
                .Where(p => ids.Contains(p.CategoryId))
                .GroupBy(p => p.CategoryId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        }
    }
}
