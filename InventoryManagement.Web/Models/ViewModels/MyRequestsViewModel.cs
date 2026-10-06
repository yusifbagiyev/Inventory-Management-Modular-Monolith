using InventoryManagement.Web.Models.DTOs;

namespace InventoryManagement.Web.Models.ViewModels
{
    public record MyRequestsViewModel
    {
        /// <summary>The latest requests, which may be fewer than the counts when the user has many.</summary>
        public List<ApprovalRequestDto> Requests { get; set; } = new();
        /// <summary>All of the user's requests by status.</summary>
        public Dictionary<string, int> StatusCounts { get; set; } = new();

        public int TotalCount => StatusCounts.Values.Sum();

        public int Count(params string[] statuses) => statuses.Sum(s => StatusCounts.GetValueOrDefault(s));
    }
}