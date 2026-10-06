using RouteService.Domain.Common;
using RouteService.Domain.Entities;
using RouteService.Domain.Enums;

namespace RouteService.Domain.Repositories
{
    public interface IInventoryRouteRepository
    {
        Task<InventoryRoute?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<IEnumerable<InventoryRoute>> GetByProductIdAsync(int productId, CancellationToken cancellationToken = default);
        /// <summary>Transfers created between from and to, projected to what the dashboard needs.</summary>
        Task<IReadOnlyList<TransferActivity>> GetTransferActivityAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
        /// <summary>Makes other transfers of this product wait until the current transaction ends.</summary>
        Task LockProductTransfersAsync(int productId, CancellationToken cancellationToken = default);
        /// <summary>True when the product already has a transfer waiting to be completed.</summary>
        Task<bool> HasPendingRouteForProductAsync(int productId, CancellationToken cancellationToken = default);
        /// <summary>The product's transfers waiting to be completed, tracked for changes.</summary>
        Task<IReadOnlyList<InventoryRoute>> GetPendingTransfersForProductAsync(int productId, CancellationToken cancellationToken = default);
        Task<InventoryRoute> AddAsync(InventoryRoute route, CancellationToken cancellationToken = default);
        Task UpdateAsync(InventoryRoute route, CancellationToken cancellationToken = default);
        /// <summary>One page of the list, counting the matches itself unless the caller already knows the total.</summary>
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
            string? departmentName = null,
            RouteListFilter? filter = null,
            int? knownTotal = null);

        /// <summary>How many routes the list's filters leave in each completion state, for its tabs.</summary>
        Task<(int Pending, int Completed)> CountByCompletionAsync(
            string? search = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int? departmentId = null,
            string? departmentName = null,
            RouteListFilter? filter = null,
            CancellationToken cancellationToken = default);

        /// <summary>Distinct department and category name pairs from both ends of the routes left by the filters.</summary>
        Task<IReadOnlyList<(string DepartmentName, string CategoryName)>> GetDepartmentCategoryPairsAsync(
            bool? isCompleted = null,
            RouteType? routeType = null,
            CancellationToken cancellationToken = default);
        Task DeleteAsync(InventoryRoute route, CancellationToken cancellationToken = default);
    }
}