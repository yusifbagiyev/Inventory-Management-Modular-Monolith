namespace InventoryManagement.Web.Models.DTOs
{
    /// <summary>The result shape the page scripts read after a form post or AJAX call.</summary>
    public record ApiResponse<T>
    {
        public bool IsSuccess { get; set; }
        public bool IsApprovalRequest { get; set; }
        public int? ApprovalRequestId { get; set; }
        public string? Message { get; set; } = string.Empty;
        public T? Data { get; set; }
    }
}