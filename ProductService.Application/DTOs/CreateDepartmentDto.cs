namespace ProductService.Application.DTOs
{
    public record CreateDepartmentDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; } = string.Empty;
        public string? DepartmentHead { get; set; } = string.Empty;
        public bool IsActive {  get; set; }
    }
}
