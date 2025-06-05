namespace RouteService.Application.Events
{
    public record ProductImageUpdatedEvent
    {
        public int ProductId { get; set; }
        public string NewImageUrl { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }
}
