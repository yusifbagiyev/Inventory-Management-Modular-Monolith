using ProductService.Domain.Entities;
using SharedServices.Events;

namespace ProductService.Application.Mappings
{
    internal static class ProductStateExtensions
    {
        /// <summary>Snapshot for events, which needs Category and Department loaded.</summary>
        public static ProductState ToState(this Product product) => new()
        {
            ProductId = product.Id,
            InventoryCode = product.InventoryCode,
            Model = product.Model,
            Vendor = product.Vendor,
            CategoryId = product.CategoryId,
            CategoryName = product.Category?.Name ?? string.Empty,
            DepartmentId = product.DepartmentId,
            DepartmentName = product.Department?.Name ?? string.Empty,
            Worker = product.Worker,
            Description = product.Description,
            IsActive = product.IsActive,
            IsWorking = product.IsWorking,
            IsNewItem = product.IsNewItem,
            ImageUrl = product.ImageUrl
        };
    }
}
