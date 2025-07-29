namespace RouteService.Application.Events
{
    public record RouteCompletedEvent
    {
        public int RouteId { get; init; }
        public int ProductId { get; init; }
        public int InventoryCode { get; init; }
        public string Model { get; init; } = string.Empty;
        public int FromDepartmentId { get; init; }
        public string FromDepartmentName { get; init; } = string.Empty;
        public int ToDepartmentId { get; init; }
        public string ToDepartmentName { get; init; } = string.Empty;
        public string ToWorker { get; init; } = string.Empty;
        public DateTime CompletedAt { get; init; } = DateTime.UtcNow;
    }
}