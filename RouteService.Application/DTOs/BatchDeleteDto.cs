namespace RouteService.Application.DTOs
{
    public record BatchDeleteDto
    {
        public List<int> RouteIds { get; set; } = new();
    }
}
