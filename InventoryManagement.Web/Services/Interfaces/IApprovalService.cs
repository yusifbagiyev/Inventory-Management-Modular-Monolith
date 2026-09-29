using InventoryManagement.Web.Models.DTOs;

namespace InventoryManagement.Web.Services.Interfaces
{
    public interface IApprovalService
    {
        Task<List<ApprovalRequestDto>> GetPendingRequestsAsync();
        Task<ApprovalRequestDto?> GetRequestDetailsAsync(int id);
        /// <returns>True when the approved action executed.</returns>
        Task<bool> ApproveRequestAsync(int id);
        Task RejectRequestAsync(int id, string reason);
        Task<ApprovalStatisticsDto> GetStatisticsAsync();
        Task<List<ApprovalRequestDto>> GetMyRequestsAsync();
        Task CancelRequestAsync(int id);
    }
}