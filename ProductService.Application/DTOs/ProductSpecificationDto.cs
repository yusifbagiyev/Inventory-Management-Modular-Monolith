namespace ProductService.Application.DTOs
{
    /// <summary>One specification line, with setters so forms and JSON can bind to it.</summary>
    public record ProductSpecificationDto
    {
        public string? Name { get; set; }
        public string? Value { get; set; }
    }
}
