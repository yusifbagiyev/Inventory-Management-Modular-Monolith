using InventoryManagement.Web.Models.DTOs;

namespace InventoryManagement.Web.Models.ViewModels
{
    public record ApprovalDashboardViewModel
    {
        public List<ApprovalRequestDto> PendingRequests { get; set; } = new();
        public int TotalPending { get; set; }
        public int TotalApproved { get; set; }
        public int TotalRejected { get; set; }
    }
}