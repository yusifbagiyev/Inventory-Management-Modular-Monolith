namespace RouteService.Domain.ValueObjects
{
    /// <summary>The product as it was before an edit, which gives an update route its source side.</summary>
    public record ExistingProduct
    (
        int ProductId,
        int InventoryCode,
        int? CategoryId,
        string? CategoryName,
        int? DepartmentId,
        string? DepartmentName,
        string? Worker,
        string? Description,
        bool? IsActive,
        bool? IsNewItem,
        bool? IsWorking
    );
}