using Microsoft.EntityFrameworkCore;
using ProductService.Infrastructure.Data;
using SharedServices.Contracts;
using SharedServices.Persistence;
using SharedServices.Storage;

namespace ProductService.Infrastructure.Services
{
    /// <summary>The products module's surface for other modules.</summary>
    public class ProductCatalog : IProductCatalog, IProductTransfers
    {
        private readonly ProductDbContext _context;
        private readonly ImageStorage _images;
        private readonly DbSession _session;

        public ProductCatalog(ProductDbContext context, ImageStorage images, DbSession session)
        {
            _context = context;
            _images = images;
            _session = session;
        }

        public Task<ProductSummary?> GetProductAsync(int productId, CancellationToken cancellationToken = default) =>
            _context.Products
                .AsNoTracking()
                .Where(p => p.Id == productId)
                .Select(p => new ProductSummary(
                    p.Id,
                    p.InventoryCode,
                    p.Model,
                    p.Vendor,
                    p.CategoryId,
                    p.Category!.Name,
                    p.DepartmentId,
                    p.Department!.Name,
                    p.IsWorking,
                    p.Worker,
                    p.ImageUrl))
                .FirstOrDefaultAsync(cancellationToken);

        public Task<DepartmentSummary?> GetDepartmentAsync(int departmentId, CancellationToken cancellationToken = default) =>
            _context.Departments
                .AsNoTracking()
                .Where(d => d.Id == departmentId)
                .Select(d => new DepartmentSummary(d.Id, d.Name))
                .FirstOrDefaultAsync(cancellationToken);

        public async Task ApplyTransferAsync(
            int productId,
            int toDepartmentId,
            string? toWorker,
            string? routeImageUrl,
            CancellationToken cancellationToken = default)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken)
                ?? throw new InvalidOperationException($"Product {productId} no longer exists");

            if (!await _context.Departments.AnyAsync(d => d.Id == toDepartmentId, cancellationToken))
                throw new InvalidOperationException($"Target department {toDepartmentId} no longer exists");

            product.UpdateAfterRouting(toDepartmentId, toWorker);

            if (!string.IsNullOrEmpty(routeImageUrl))
            {
                var copy = await _images.CopyAsync(routeImageUrl, ImageStorage.Products, product.InventoryCode, cancellationToken);
                if (copy != null)
                {
                    var oldImageUrl = product.ImageUrl;
                    product.UpdateImage(copy);
                    _session.OnRollback(() => _images.DeleteAsync(copy));
                    if (!string.IsNullOrEmpty(oldImageUrl))
                        _session.AfterCommit((_, _) => _images.DeleteAsync(oldImageUrl));
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
