using RouteService.Application.DTOs;

namespace RouteService.Application.Interfaces
{
    public interface IProductServiceClient
    {
        Task<ProductInfoDto?> GetProductByIdAsync(int productId, CancellationToken cancellationToken = default);
        Task<DepartmentDto?> GetDepartmentByIdAsync(int departmentId, CancellationToken cancellationToken = default);
        Task<bool> UpdateProductStatusAsync(int productId, bool isActive, CancellationToken cancellationToken = default);
        Task<bool> UpdateProductInfoAfterRouting(int productId, int departmentId, string? worker, CancellationToken cancellationToken = default);
    }
}