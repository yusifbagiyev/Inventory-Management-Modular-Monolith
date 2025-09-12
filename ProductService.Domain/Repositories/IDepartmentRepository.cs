using ProductService.Domain.Common;
using ProductService.Domain.Entities;

namespace ProductService.Domain.Repositories
{
    public interface IDepartmentRepository
    {
        Task<Department?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<IEnumerable<Department>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<PagedResult<Department>> GetPagedAsync(int pageNumber, int pageSize,string search, CancellationToken cancellationToken = default);
        Task<Department> AddAsync(Department category, CancellationToken cancellationToken = default);
        Task UpdateAsync(Department category, CancellationToken cancellationToken = default);
        Task DeleteAsync(Department category, CancellationToken cancellationToken = default);
        Task<bool> ExistsByIdAsync(int id, CancellationToken cancellationToken = default);
    }
}