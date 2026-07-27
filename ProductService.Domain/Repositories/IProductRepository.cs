using ProductService.Domain.Common;
using ProductService.Domain.Entities;

namespace ProductService.Domain.Repositories
{
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<PagedResult<Product>> GetAllAsync(
            int pageNumber=1,
            int pageSize=30,
            string? search=null,
            DateTime? startDate=null,
            DateTime? endDate = null,
            bool? status=null,
            bool? availability = null,
            int? categoryId=null,
            int? departmentId=null,
            bool? hasImage=null,
            bool? assigned=null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Distinct (departmentId, categoryId) combinations that actually occur in the inventory.
        /// Drives the cascading list filters so a department only offers the categories present in it.
        /// </summary>
        Task<IReadOnlyList<(int DepartmentId, int CategoryId)>> GetDepartmentCategoryPairsAsync(
            CancellationToken cancellationToken = default);

        Task<IEnumerable<Product>> GetByCategoryIdAsync(int categoryId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Product>> GetByDepartmentIdAsync(int departmentId, CancellationToken cancellationToken = default);
        Task<Product?> GetByInventoryCodeAsync(int inventoryCode, CancellationToken cancellationToken = default);
        Task<Product> AddAsync(Product product, CancellationToken cancellationToken = default);
        Task UpdateAsync(Product product, CancellationToken cancellationToken = default);
        Task DeleteAsync(Product product, CancellationToken cancellationToken = default);
        Task<bool> ExistsByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<int> CountByDepartmentIdAsync(int departmentId, CancellationToken cancellationToken = default);
        Task<int> CountByCategoryIdAsync(int categoryId, CancellationToken cancellationToken = default);
    }
}