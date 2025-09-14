using RouteService.Domain.Common;
using RouteService.Domain.Entities;
using RouteService.Domain.Enums;

namespace RouteService.Domain.Repositories
{
    public interface IInventoryRouteRepository
    {
        Task<InventoryRoute?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<IEnumerable<InventoryRoute>> GetByProductIdAsync(int productId, CancellationToken cancellationToken = default);
        Task<IEnumerable<InventoryRoute>> GetByDepartmentIdAsync(int departmentId, CancellationToken cancellationToken = default);
        Task<IEnumerable<InventoryRoute>> GetByRouteTypeAsync(RouteType routeType, CancellationToken cancellationToken = default);
        Task<InventoryRoute> AddAsync(InventoryRoute route, CancellationToken cancellationToken = default);
        Task UpdateAsync(InventoryRoute route, CancellationToken cancellationToken = default);
        Task<InventoryRoute?> GetLatestRouteForProductAsync(int productId, CancellationToken cancellationToken = default);
        Task<PagedResult<InventoryRoute>> GetAllAsync(
            int pageNumber = 1,
            int pageSize = 30,
            string? search= null,
            bool? isCompleted = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            CancellationToken cancellationToken = default);
        Task<IEnumerable<InventoryRoute>> GetIncompleteRoutesAsync(CancellationToken cancellationToken = default);
        Task DeleteAsync(InventoryRoute route, CancellationToken cancellationToken = default);
        Task<InventoryRoute?> GetPreviousRouteForProductAsync(int productId, int currentRouteId, CancellationToken cancellationToken = default);
    }
}