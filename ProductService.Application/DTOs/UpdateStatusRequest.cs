namespace ProductService.Application.DTOs
{
    public record UpdateStatusRequest
    {
        public bool IsActive { get; set; }
    }
}
