using ProductService.Application.DTOs;


namespace ProductService.Application.Interfaces
{
    public interface IProductManagementService
    {
        Task<ProductDto> CreateProductWithApprovalAsync(CreateProductDto dto, int userId, string userName, List<string> userPermissions);
        Task<ProductDto> UpdateProductWithApprovalAsync(int id, UpdateProductDto dto, int userId, string userName, List<string> userPermissions);
        Task DeleteProductWithApprovalAsync(int id, int userId, string userName, List<string> userPermissions);
    }
}