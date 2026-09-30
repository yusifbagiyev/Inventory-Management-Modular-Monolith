using MediatR;

namespace SharedServices.Events
{
    public record RouteCompletedEvent : INotification
    {
        public int RouteId { get; init; }
        public int ProductId { get; init; }
        public int InventoryCode { get; init; }
        public string Model { get; init; } = string.Empty;
        public string Vendor { get; init; } = string.Empty;
        public string CategoryName { get; init; } = string.Empty;
        public string FromDepartmentName { get; init; } = string.Empty;
        public string? FromWorker { get; init; }
        public int ToDepartmentId { get; init; }
        public string ToDepartmentName { get; init; } = string.Empty;
        public string? ToWorker { get; init; }
        public string? Notes { get; init; }
        public string? ImageUrl { get; init; }
        public DateTime CompletedAt { get; init; }
    }
}
