using RouteService.Domain.Common;
using RouteService.Domain.Entities;
using RouteService.Domain.Enums;

namespace RouteService.Domain.Repositories
{
    public interface IInventoryRouteRepository
    {
        Task<InventoryRoute?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<IEnumerable<InventoryRoute>> GetByProductIdAsync(int productId, CancellationToken cancellationToken = default);
        /// <summary>True when the product already has a transfer waiting to be completed.</summary>
        /// <summary>Transfers created in [from, to], projected to what the dashboard needs.</summary>
        Task<IReadOnlyList<TransferActivity>> GetTransferActivityAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
        Task<bool> HasPendingRouteForProductAsync(int productId, CancellationToken cancellationToken = default);
        Task<InventoryRoute> AddAsync(InventoryRoute route, CancellationToken cancellationToken = default);
        Task UpdateAsync(InventoryRoute route, CancellationToken cancellationToken = default);
        Task<PagedResult<InventoryRoute>> GetAllAsync(
            int pageNumber = 1,
            int pageSize = 30,
            string? search= null,
            bool? isCompleted = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int? departmentId = null,
            string? categoryName = null,
            RouteType? routeType = null,
            CancellationToken cancellationToken = default,
            string? departmentName = null);

        /// <summary>
        /// Distinct (departmentId, categoryName) combinations across routes, taking BOTH ends of each
        /// transfer. Drives the cascading list filters. Routes store the category as a name (the
        /// product snapshot has no id), so the category side is name-based. The status/type filters
        /// are applied first, so the department and category options narrow to the current selection.
        /// </summary>
        Task<IReadOnlyList<(string DepartmentName, string CategoryName)>> GetDepartmentCategoryPairsAsync(
            bool? isCompleted = null,
            RouteType? routeType = null,
            CancellationToken cancellationToken = default);
        Task DeleteAsync(InventoryRoute route, CancellationToken cancellationToken = default);
    }
}