using Microsoft.AspNetCore.Http;

namespace RouteService.Application.DTOs
{
    public record TransferInventoryDto
    {
        public int ProductId { get; set; }
        public int ToDepartmentId { get; set; }
        public string? ToWorker { get; set; } = string.Empty;
        /// <summary>Single image (older clients). Combined with <see cref="ImageFiles"/>, it comes first.</summary>
        public IFormFile? ImageFile { get; set; }
        /// <summary>Photos of the item as handed over, in order; on completion they become the product's images.</summary>
        public List<IFormFile>? ImageFiles { get; set; }
        public string? Notes { get; set; }
    }
}
