namespace ProductService.Domain.Entities
{
    /// <summary>One line of a product's specifications ("RAM" = "16 GB"), stored with the product.</summary>
    public sealed record ProductSpecification(string Name, string Value);
}
