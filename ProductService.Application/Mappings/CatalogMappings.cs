using ProductService.Application.DTOs;
using ProductService.Domain.Entities;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Mappings
{
    public static class CatalogMappings
    {
        /// <summary>Category and Department must be loaded for the names.</summary>
        public static ProductDto ToDto(this Product product) => new()
        {
            Id = product.Id,
            InventoryCode = product.InventoryCode,
            Model = product.Model,
            Vendor = product.Vendor,
            ImageUrl = string.IsNullOrEmpty(product.ImageUrl) ? null : product.ImageUrl,
            ImageUrls = product.ImageUrls.ToList(),
            Description = product.Description,
            Worker = product.Worker,
            IsWorking = product.IsWorking,
            IsActive = product.IsActive,
            IsNewItem = product.IsNewItem,
            CategoryId = product.CategoryId,
            CategoryName = product.Category?.Name,
            DepartmentId = product.DepartmentId,
            DepartmentName = product.Department?.Name,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };

        public static CategoryDto ToDto(this Category category, int productCount) => new()
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            IsActive = category.IsActive,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt,
            ProductCount = productCount
        };

        public static DepartmentDto ToDto(this Department department, int productCount, int workerCount) => new()
        {
            Id = department.Id,
            Name = department.Name,
            Description = department.Description,
            DepartmentHead = department.DepartmentHead,
            IsActive = department.IsActive,
            CreatedAt = department.CreatedAt,
            UpdatedAt = department.UpdatedAt,
            ProductCount = productCount,
            WorkerCount = workerCount
        };

        /// <summary>Maps a page of categories with their product counts from one grouped query.</summary>
        public static async Task<List<CategoryDto>> ToDtosAsync(
            this IEnumerable<Category> categories, ICategoryRepository repository, CancellationToken cancellationToken)
        {
            var list = categories.ToList();
            var counts = await repository.GetProductCountsAsync(list.Select(c => c.Id), cancellationToken);
            return list.Select(c => c.ToDto(counts.GetValueOrDefault(c.Id))).ToList();
        }

        /// <summary>Maps a page of departments with product/worker counts from one grouped query.</summary>
        public static async Task<List<DepartmentDto>> ToDtosAsync(
            this IEnumerable<Department> departments, IDepartmentRepository repository, CancellationToken cancellationToken)
        {
            var list = departments.ToList();
            var usage = await repository.GetUsageAsync(list.Select(d => d.Id), cancellationToken);
            return list.Select(d =>
            {
                var u = usage.GetValueOrDefault(d.Id);
                return d.ToDto(u.Products, u.Workers);
            }).ToList();
        }
    }
}
