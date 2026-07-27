namespace ProductService.Application.DTOs
{
    /// <summary>
    /// The distinct (department, category) combinations that actually occur in the inventory.
    /// The list screen uses this to make its Department and Category filters cascade - selecting a
    /// department hides the categories that department has no products in, and vice versa.
    /// </summary>
    public class ProductFilterFacetsDto
    {
        public List<DepartmentCategoryFacetDto> Pairs { get; set; } = new();
    }

    public class DepartmentCategoryFacetDto
    {
        public int DepartmentId { get; set; }
        public int CategoryId { get; set; }
    }
}
