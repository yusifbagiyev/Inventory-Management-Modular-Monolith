using InventoryManagement.Web.Models.DTOs;

namespace InventoryManagement.Web.Services.Interfaces
{
    public interface IApprovalService
    {
        Task<List<ApprovalRequestDto>> GetPendingRequestsAsync();
        Task<ApprovalRequestDto?> GetRequestDetailsAsync(int id);
        Task ApproveRequestAsync(int id);
        Task RejectRequestAsync(int id, string reason);
        Task<ApprovalRequestDto> CreateApprovalRequestAsync(CreateApprovalRequestDto dto, int userId, string userName);
        Task<ApprovalStatisticsDto> GetStatisticsAsync();
        Task<List<ApprovalRequestDto>> GetMyRequestsAsync();
        Task CancelRequestAsync(int id);
    }
}