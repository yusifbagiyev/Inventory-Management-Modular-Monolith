using Microsoft.AspNetCore.Http;

namespace RouteService.Application.DTOs
{
    public record UpdateRouteDto
    {
        /// <summary>Single image from older clients that replaces all current images.</summary>
        public IFormFile? ImageFile { get; set; }
        /// <summary>Images to add after the current ones.</summary>
        public List<IFormFile>? ImageFiles { get; set; }
        /// <summary>Urls of current images to remove.</summary>
        public List<string>? RemoveImageUrls { get; set; }
        /// <summary>Url of the image to make the cover, or null to keep the current one.</summary>
        public string? CoverImageUrl { get; set; }
        /// <summary>New destination department, or null to leave it unchanged.</summary>
        /// <remarks>The department name is looked up on the server from this id.</remarks>
        public int? ToDepartmentId { get; set; }
        public string? ToWorker { get; set; }
        public string? Notes { get; set; }
    }
}