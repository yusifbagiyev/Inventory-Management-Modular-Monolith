using Microsoft.AspNetCore.Http;

namespace RouteService.Application.DTOs
{
    public record UpdateRouteDto
    {
        public IFormFile? ImageFile { get; set; }
        public string? ToWorker { get; set; }
        public string? Notes { get; set; }
    }
}