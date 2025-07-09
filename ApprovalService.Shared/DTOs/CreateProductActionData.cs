namespace ApprovalService.Shared.DTOs
{
    public record CreateProductActionData
    {
        public object ProductData { get; set; } = null!;
    }
}