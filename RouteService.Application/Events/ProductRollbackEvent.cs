namespace RouteService.Application.Events
{
    public record ProductRollbackEvent
    {
        public int ProductId { get; set; }
        public int ToDepartmentId { get; set; }
        public string? ToWorker { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime RolledBackAt { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}