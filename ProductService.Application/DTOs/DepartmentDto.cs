namespace ProductService.Application.DTOs
{
    public record DepartmentDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; } = string.Empty;
        public string? DepartmentHead { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? ProductCount { get; set; }
        public int? WorkerCount { get; set; }
    }
}