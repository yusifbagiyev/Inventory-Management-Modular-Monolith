namespace ApprovalService.Application.DTOs
{
    public record CreateProductActionData
    {
        public object ProductData { get; set; } = null!;
    }
    public record UpdateProductActionData
    {
        public int ProductId { get; set; }
        public object UpdateData { get; set; } = null!;
    }

    public record DeleteProductActionData
    {
        public int ProductId { get; set; }
    }

    public record DeleteRouteActionData
    {
        public int RouteId { get; set; }
    }
}