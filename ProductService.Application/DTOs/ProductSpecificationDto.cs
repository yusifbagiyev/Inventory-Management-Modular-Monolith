namespace ProductService.Application.DTOs
{
    /// <summary>One specification line ("RAM" = "16 GB"). Settable, so forms and JSON bind to it.</summary>
    public record ProductSpecificationDto
    {
        public string? Name { get; set; }
        public string? Value { get; set; }
    }
}
