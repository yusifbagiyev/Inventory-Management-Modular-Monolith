namespace ProductService.Application.DTOs
{
    public record CreateCategoryDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
