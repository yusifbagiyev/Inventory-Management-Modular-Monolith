using InventoryManagement.Web.Models.DTOs;

namespace InventoryManagement.Web.Models.ViewModels
{
    public record ApprovalDashboardViewModel
    {
        /// <summary>pending, approved or rejected: the open tab.</summary>
        public string Tab { get; set; } = "pending";
        /// <summary>The open tab's requests.</summary>
        public List<ApprovalRequestDto> Requests { get; set; } = new();
        public int TotalPending { get; set; }
        /// <summary>Decided today (the header figures).</summary>
        public int TotalApproved { get; set; }
        public int TotalRejected { get; set; }
        /// <summary>All-time counts for the tabs.</summary>
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
    }
}