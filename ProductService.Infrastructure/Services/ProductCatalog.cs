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
                .Select(d => new DepartmentSummary(d.Id, d.Name, d.IsActive))
                .FirstOrDefaultAsync(cancellationToken);

        public async Task ApplyTransferAsync(
            int productId,
            int toDepartmentId,
            string? toWorker,
            IReadOnlyList<string> routeImageUrls,
            CancellationToken cancellationToken = default)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken)
                ?? throw new InvalidOperationException($"Product {productId} no longer exists");

            if (!await _context.Departments.AnyAsync(d => d.Id == toDepartmentId, cancellationToken))
                throw new InvalidOperationException($"Target department {toDepartmentId} no longer exists");

            product.UpdateAfterRouting(toDepartmentId, toWorker);

            // The route's photos show the item as handed over, so they replace the product's images
            var copies = new List<string>();
            foreach (var routeImageUrl in routeImageUrls)
            {
                var copy = await _images.CopyAsync(routeImageUrl, ImageStorage.Products, product.InventoryCode, cancellationToken);
                if (copy == null) continue;
                _session.OnRollback(() => _images.DeleteAsync(copy));
                copies.Add(copy);
            }
            if (copies.Count > 0)
            {
                var oldImages = product.ImageUrls.ToList();
                product.SetImages(copies);
                foreach (var url in oldImages)
                    _session.AfterCommit((_, _) => _images.DeleteAsync(url));
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
