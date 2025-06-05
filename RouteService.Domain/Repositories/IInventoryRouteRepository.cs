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
    }
}
