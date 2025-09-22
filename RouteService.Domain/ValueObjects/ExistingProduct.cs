namespace RouteService.Domain.ValueObjects
{
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