using Microsoft.AspNetCore.Http;

namespace RouteService.Application.DTOs
{
    public record UpdateRouteDto
    {
        public IFormFile? ImageFile { get; set; }
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