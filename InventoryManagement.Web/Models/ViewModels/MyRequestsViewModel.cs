using InventoryManagement.Web.Models.DTOs;

namespace InventoryManagement.Web.Models.ViewModels
{
    public record MyRequestsViewModel
    {
        public List<ApprovalRequestDto> Requests { get; set; } = new();
        public Dictionary<string, int> StatusCounts { get; set; } = new();
    }
}