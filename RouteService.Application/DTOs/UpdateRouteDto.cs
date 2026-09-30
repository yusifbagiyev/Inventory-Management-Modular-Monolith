using Microsoft.AspNetCore.Http;

namespace RouteService.Application.DTOs
{
    public record UpdateRouteDto
    {
        /// <summary>Single image (older clients): replaces all current images.</summary>
        public IFormFile? ImageFile { get; set; }
        /// <summary>Images to add after the current ones.</summary>
        public List<IFormFile>? ImageFiles { get; set; }
        /// <summary>Current images (by url) to remove.</summary>
        public List<string>? RemoveImageUrls { get; set; }
        /// <summary>A current image (url) to make the cover; null keeps the current cover.</summary>
        public string? CoverImageUrl { get; set; }
        /// <summary>
        /// New destination department. Null means "leave unchanged". Until this field existed the
        /// edit screen's department value was silently dropped by model binding. The department NAME
        /// is resolved server-side from this id - never trusted from the client.
        /// </summary>
        public int? ToDepartmentId { get; set; }
        public string? ToWorker { get; set; }
        public string? Notes { get; set; }
    }
}