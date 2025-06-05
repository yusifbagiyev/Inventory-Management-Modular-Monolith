using Microsoft.AspNetCore.Http;

namespace RouteService.Application.DTOs.Commands
{
    public record TransferInventoryDto
    {
        public int ProductId { get; set; }
        public int ToDepartmentId { get; set; }
        public string FromWorker { get; set; } = string.Empty;
        public string ToWorker { get; set; } = string.Empty;
        public IFormFile? ImageFile { get; set; }
        public string? Notes { get; set; }
    }
}
