using Microsoft.AspNetCore.Http;

namespace ProductService.Application.DTOs
{
    public record UpdateProductDto
    {
        public string? Model { get; set; } = string.Empty;
        public string? Vendor { get; set; } = string.Empty;
        public string? Worker { get; set; } = string.Empty;
        /// <summary>Single image from older clients that replaces all current images.</summary>
        public IFormFile? ImageFile { get; set; }
        /// <summary>Images to add after the current ones.</summary>
        public List<IFormFile>? ImageFiles { get; set; }
        /// <summary>Urls of current images to remove.</summary>
        public List<string>? RemoveImageUrls { get; set; }
        /// <summary>Url of the image to make the cover, or null to keep the current one.</summary>
        public string? CoverImageUrl { get; set; }
        public string? Description { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public int DepartmentId { get; set; }
        public bool IsWorking { get; set; }
        public bool IsActive { get; set; }
        public bool IsNewItem { get; set; }
        /// <summary>True when <see cref="Color"/> and <see cref="Specifications"/> were sent and replace the current ones.</summary>
        /// <remarks>Older clients and old approval requests leave it false, so the product keeps its details.</remarks>
        public bool ReplaceDetails { get; set; }
        public string? Color { get; set; }
        public List<ProductSpecificationDto>? Specifications { get; set; }
    }
}