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
        /// <summary>Latest approved or rejected requests, none when approved is null, but always with both tab counts.</summary>
        Task<(List<ApprovalRequestDto> Items, int ApprovedCount, int RejectedCount)> GetDecidedRequestsAsync(bool? approved, int take);
        Task<List<ApprovalRequestDto>> GetMyRequestsAsync();
        Task CancelRequestAsync(int id);
    }
}