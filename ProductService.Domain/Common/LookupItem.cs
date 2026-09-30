namespace ProductService.Domain.Common
{
    /// <summary>Id/name pair for dropdowns and filters.</summary>
    /// <param name="IsActive">Inactive items stay in filters but are not offered for new assignments.</param>
    public record LookupItem(int Id, string Name, bool IsActive = true);
}
