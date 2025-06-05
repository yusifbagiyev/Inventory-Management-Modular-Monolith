namespace ProductService.Application.DTOs
{
    public record UpdateProductRequest
    {
        public int DepartmentId { get; set; }
        public string? Worker { get; set; }
    }
}
