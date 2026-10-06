using ProductService.Domain.Common;
using ProductService.Domain.Entities;

namespace ProductService.Domain.Repositories
{
    public interface IDepartmentRepository
    {
        Task<Department?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<IEnumerable<Department>> GetAllAsync(CancellationToken cancellationToken = default);
        /// <summary>Id and name only, ordered by name.</summary>
        Task<IReadOnlyList<LookupItem>> GetLookupAsync(CancellationToken cancellationToken = default);
        Task<(int Active, int Inactive)> CountByActivityAsync(CancellationToken cancellationToken = default);
        /// <summary>How many departments have at least one product.</summary>
        Task<int> CountWithProductsAsync(CancellationToken cancellationToken = default);
        Task<PagedResult<Department>> GetPagedAsync(int pageNumber, int pageSize, string? search, CancellationToken cancellationToken = default);
        Task<Department> AddAsync(Department category, CancellationToken cancellationToken = default);
        Task UpdateAsync(Department category, CancellationToken cancellationToken = default);
        Task DeleteAsync(Department category, CancellationToken cancellationToken = default);
        /// <summary>Products and distinct assigned workers per department for the given ids.</summary>
        Task<Dictionary<int, (int Products, int Workers)>> GetUsageAsync(IEnumerable<int> departmentIds, CancellationToken cancellationToken = default);
    }
}