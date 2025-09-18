using RouteService.Application.DTOs;

namespace RouteService.Application.Interfaces
{
    public interface IRouteManagementService
    {
        Task<InventoryRouteDto> TransferInventoryWithApprovalAsync(
            TransferInventoryDto dto, int userId, string userName, List<string> userPermissions);
        Task UpdateRouteWithApprovalAsync(
            int id, UpdateRouteDto dto, int userId, string userName, List<string> userPermissions);
        Task DeleteRouteWithApprovalAsync(
            int id, int userId, string userName, List<string> userPermissions);
    }
}