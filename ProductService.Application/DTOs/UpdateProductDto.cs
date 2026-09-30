using Microsoft.AspNetCore.Http;

namespace ProductService.Application.DTOs
{
    public record UpdateProductDto
    {
        public string? Model { get; set; } = string.Empty;
        public string? Vendor { get; set; } = string.Empty;
        public string? Worker { get; set; } = string.Empty;
        /// <summary>Single image (older clients): replaces all current images.</summary>
        public IFormFile? ImageFile { get; set; }
        /// <summary>Images to add after the current ones.</summary>
        public List<IFormFile>? ImageFiles { get; set; }
        /// <summary>Current images (by url) to remove.</summary>
        public List<string>? RemoveImageUrls { get; set; }
        /// <summary>A current image (url) to make the cover; null keeps the current cover.</summary>
        public string? CoverImageUrl { get; set; }
        public string? Description { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public int DepartmentId { get; set; }
        public bool IsWorking { get; set; }
        public bool IsActive { get; set; }
        public bool IsNewItem { get; set; }
        /// <summary>
        /// True when <see cref="Color"/> and <see cref="Specifications"/> were sent and replace the
        /// current ones. Callers that do not know these fields (older clients, approval requests made
        /// before they existed) leave it false and the product keeps its colour and specifications.
        /// </summary>
        public bool ReplaceDetails { get; set; }
        public string? Color { get; set; }
        public List<ProductSpecificationDto>? Specifications { get; set; }
    }
}