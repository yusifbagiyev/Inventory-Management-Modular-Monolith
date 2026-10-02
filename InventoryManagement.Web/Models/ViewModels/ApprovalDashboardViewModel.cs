using InventoryManagement.Web.Models.DTOs;

namespace InventoryManagement.Web.Models.ViewModels
{
    public record ApprovalDashboardViewModel
    {
        /// <summary>The open tab, one of pending, approved or rejected.</summary>
        public string Tab { get; set; } = "pending";
        public List<ApprovalRequestDto> Requests { get; set; } = new();
        public int TotalPending { get; set; }
        /// <summary>Decided today, for the header figures.</summary>
        public int TotalApproved { get; set; }
        public int TotalRejected { get; set; }
        /// <summary>All-time counts for the tabs.</summary>
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
    }
}