using System.ComponentModel.DataAnnotations;
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
        /// <summary>New destination department or null to keep it, the server looks up its name.</summary>
        public int? ToDepartmentId { get; set; }
        /// <summary>Null keeps the worker and an empty value clears it, so a posted empty field must stay empty.</summary>
        [DisplayFormat(ConvertEmptyStringToNull = false)]
        public string? ToWorker { get; set; }
        /// <summary>Null keeps the notes and an empty value clears them.</summary>
        [DisplayFormat(ConvertEmptyStringToNull = false)]
        public string? Notes { get; set; }
    }
}