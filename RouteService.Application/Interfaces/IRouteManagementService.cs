using RouteService.Application.DTOs;
using System.Security.Claims;

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

        int GetUserId(ClaimsPrincipal User);

        string GetUserName(ClaimsPrincipal User);

        List<string> GetUserPermissions(ClaimsPrincipal User);
    }
}