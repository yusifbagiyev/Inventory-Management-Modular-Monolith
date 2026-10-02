namespace ProductService.Domain.Entities
{
    /// <summary>One name and value line of a product's specifications.</summary>
    public sealed record ProductSpecification(string Name, string Value);
}
