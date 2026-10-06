namespace RouteService.Domain.ValueObjects
{
    /// <summary>The product as it was before an edit, which gives an update route its source side.</summary>
    public record ExistingProduct(int? DepartmentId, string? DepartmentName, string? Worker);
}