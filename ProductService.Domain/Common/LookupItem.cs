namespace ProductService.Domain.Common
{
    /// <summary>Id/name pair for dropdowns and filters, where inactive items stay in filters but not in pick lists.</summary>
    public record LookupItem(int Id, string Name, bool IsActive = true);
}
