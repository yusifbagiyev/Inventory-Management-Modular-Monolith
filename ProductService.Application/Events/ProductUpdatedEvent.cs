using ProductService.Application.DTOs;

namespace ProductService.Application.Events
{
    public record ProductUpdatedEvent
    {
        public ProductDto Product { get; set; }=null!;
        public string? Changes { get; set; } = string.Empty;
        public byte[]? ImageData { get; set; } = null;
        public string? ImageFileName { get; set; } = null;
        public DateTime UpdatedAt { get; set; }
    }
}