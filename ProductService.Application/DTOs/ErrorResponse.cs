namespace ProductService.Application.DTOs
{
    public record ErrorResponse
    {
        public string Error { get; set; } = string.Empty;
        public string? Details { get; set; }
        public Dictionary<string, string[]>? ValidationErrors { get; set; }
    }
}