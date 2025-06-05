using RouteService.Domain.Enums;

namespace RouteService.Application.DTOs
{
    public record InventoryRouteDto
    {
        public int Id { get; set; }
        public RouteType RouteType { get; set; }
        public string RouteTypeName => RouteType.ToString();
        public int ProductId { get; set; }
        public int InventoryCode { get; set; }
        public string Model { get; set; } = string.Empty;
        public string Vendor { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int? FromDepartmentId { get; set; }
        public string? FromDepartmentName { get; set; }
        public int ToDepartmentId { get; set; }
        public string ToDepartmentName { get; set; } = string.Empty;
        public string? FromWorker { get; set; }
        public string ToWorker { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string? Notes { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
