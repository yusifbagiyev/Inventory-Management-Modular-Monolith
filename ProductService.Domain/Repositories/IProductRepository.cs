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
            ProductListFilter? filter = null,
            CancellationToken cancellationToken = default);

        /// <summary>Distinct department and category pairs left after the state and quick filters.</summary>
        Task<IReadOnlyList<(int DepartmentId, int CategoryId)>> GetDepartmentCategoryPairsAsync(
            bool? status = null,
            bool? availability = null,
            bool? hasImage = null,
            bool? assigned = null,
            CancellationToken cancellationToken = default);

        Task<IEnumerable<Product>> GetByCategoryIdAsync(int categoryId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Product>> GetByDepartmentIdAsync(int departmentId, CancellationToken cancellationToken = default);
        Task<Product?> GetByInventoryCodeAsync(int inventoryCode, CancellationToken cancellationToken = default);
        Task<Product> AddAsync(Product product, CancellationToken cancellationToken = default);
        Task UpdateAsync(Product product, CancellationToken cancellationToken = default);
        Task DeleteAsync(Product product, CancellationToken cancellationToken = default);
        Task<bool> ExistsByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<int> CountAsync(CancellationToken cancellationToken = default);
        /// <summary>Product counts, optionally only for products created in the given range.</summary>
        Task<(int Total, int Active, int NotWorking)> CountCreatedAsync(DateTime? createdFrom, DateTime? createdTo, CancellationToken cancellationToken = default);
        Task<int> CountByDepartmentIdAsync(int departmentId, CancellationToken cancellationToken = default);
        Task<int> CountByCategoryIdAsync(int categoryId, CancellationToken cancellationToken = default);

        // Soft-deleted products, which every other method leaves out
        Task<(IReadOnlyList<Product> Items, int TotalCount)> GetDeletedAsync(string? search, int pageNumber, int pageSize, ProductListFilter? filter = null, CancellationToken cancellationToken = default);
        /// <summary>Names of the users who deleted products, for the deleted list's filter.</summary>
        Task<IReadOnlyList<string>> GetDeletedByNamesAsync(CancellationToken cancellationToken = default);
        Task<Product?> GetDeletedByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<int> CountDeletedByDepartmentIdAsync(int departmentId, CancellationToken cancellationToken = default);
        Task<int> CountDeletedByCategoryIdAsync(int categoryId, CancellationToken cancellationToken = default);
    }
}