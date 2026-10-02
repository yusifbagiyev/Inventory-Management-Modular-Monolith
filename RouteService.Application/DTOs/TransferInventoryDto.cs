using Microsoft.AspNetCore.Http;

namespace RouteService.Application.DTOs
{
    public record TransferInventoryDto
    {
        public int ProductId { get; set; }
        public int ToDepartmentId { get; set; }
        public string? ToWorker { get; set; } = string.Empty;
        /// <summary>Single image from older clients, placed before <see cref="ImageFiles"/>.</summary>
        public IFormFile? ImageFile { get; set; }
        /// <summary>Photos of the item as handed over, which become the product's images on completion.</summary>
        public List<IFormFile>? ImageFiles { get; set; }
        public string? Notes { get; set; }
    }
}
