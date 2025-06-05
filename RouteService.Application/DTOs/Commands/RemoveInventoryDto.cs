using Microsoft.AspNetCore.Http;

namespace RouteService.Application.DTOs.Commands
{
    public record RemoveInventoryDto
    {
        public int ProductId { get; set; }
        public string FromWorker { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public IFormFile? ImageFile { get; set; }
    }
}
