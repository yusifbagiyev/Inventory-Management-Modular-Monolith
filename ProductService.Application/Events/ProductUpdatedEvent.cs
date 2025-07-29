namespace ProductService.Application.Events
{
    public record ProductUpdatedEvent
    {
        public int ProductId { get; set; }
        public int InventoryCode { get; set; }
        public string? Changes { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }
}