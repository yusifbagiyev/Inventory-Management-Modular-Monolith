namespace InventoryManagement.Web.Models.DTOs
{
    /// <summary>
    /// Mirror of the products API facets payload: the (department, category) pairs that exist in the
    /// inventory. Feeds the cascading Department/Category filters on the product list.
    /// </summary>
    public record ProductFilterFacetsDto
    {
        public List<DepartmentCategoryFacetDto> Pairs { get; set; } = new();
    }

    public record DepartmentCategoryFacetDto
    {
        public int DepartmentId { get; set; }
        public int CategoryId { get; set; }
    }
}
