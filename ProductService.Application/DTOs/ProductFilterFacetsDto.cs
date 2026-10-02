namespace ProductService.Application.DTOs
{
    /// <summary>Department and category pairs that occur in the inventory, so the list filters can cascade.</summary>
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
