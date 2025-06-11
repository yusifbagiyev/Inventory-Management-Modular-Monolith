namespace ProductService.Application.Events
{
    public record ProductImageUpdatedEvent
    {
        public int ProductId { get; set; }
        public byte[]? ImageData { get; set; }
        public string? ImageFileName { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
