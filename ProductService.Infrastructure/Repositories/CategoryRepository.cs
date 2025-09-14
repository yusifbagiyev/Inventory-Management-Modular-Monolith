using Microsoft.EntityFrameworkCore;
using ProductService.Domain.Common;
using ProductService.Domain.Entities;
using ProductService.Domain.Repositories;
using ProductService.Infrastructure.Data;

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
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        }

        public async Task<IEnumerable<Category>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Categories
                .Include(p=>p.Products)
                .ToListAsync(cancellationToken);
        }


        public async Task<PagedResult<Category>> GetPagedAsync(
            int pageNumber, int pageSize, string? search, CancellationToken cancellationToken = default)
        {
            var query = _context.Categories.Include(c => c.Products).AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                search = search.Trim();
                query = query.Where(r =>
                    EF.Functions.ILike(r.Name, $"%{search}%") ||
                    EF.Functions.ILike(r.Description, $"%{search}%")
                );
            }

            var totalCount= await query.CountAsync(cancellationToken);

            var items=await query
                .OrderBy(n=>n.Name)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<Category>
            {
                Items = items,
                TotalCount=totalCount,
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

        public async Task<bool> ExistsByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Categories.AnyAsync(c => c.Id == id, cancellationToken);
        }

        public async Task<int?> GetProductCountAsync(int categoryId, CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .Include(c=>c.Category)
                .Where(c=>c.CategoryId==categoryId)
                .CountAsync(cancellationToken);
        }
    }
}