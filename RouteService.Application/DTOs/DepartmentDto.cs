namespace RouteService.Application.DTOs
{
    public record DepartmentDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
