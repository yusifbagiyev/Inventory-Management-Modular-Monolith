using Microsoft.EntityFrameworkCore;
using ProductService.Application.DTOs;
using ProductService.Domain.Common;
using ProductService.Domain.Entities;
using ProductService.Domain.Repositories;
using ProductService.Infrastructure.Data;

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
            DateTime? startDate,
            DateTime? endDate,
            bool? status,
            bool? availability,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Products
                .Include(p=>p.Category)
                .Include(p=>p.Department)
                .AsQueryable();

            if (status.HasValue)
                query=query.Where(p=>p.IsWorking==status.Value);

            if (availability.HasValue)
                query = query.Where(p => p.IsActive == availability.Value);

            if (startDate.HasValue)
            {;
                query = query.Where(r => r.CreatedAt >= startDate);
            }

            if (endDate.HasValue)
            {
                var EndDate = endDate.Value.AddDays(1).AddSeconds(-1);
                query = query.Where(r => r.CreatedAt <= EndDate);
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<Product>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
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
    }
}