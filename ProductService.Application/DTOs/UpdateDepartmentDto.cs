namespace ProductService.Application.DTOs
{
    public record UpdateDepartmentDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
