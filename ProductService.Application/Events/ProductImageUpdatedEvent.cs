namespace ProductService.Application.Events
{
    public class ProductImageUpdatedEvent
    {
        public int ProductId { get; set; }
        public string NewImageUrl { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }
}
