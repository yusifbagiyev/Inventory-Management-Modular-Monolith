namespace InventoryManagement.Web.Models.DTOs
{
    /// <summary>
    /// Mirror of the routes API facets payload: the (department, category-name) pairs that exist
    /// across routes. Feeds the cascading Department/Category filters on the route list. Category is
    /// a name because routes store only the category name, not an id.
    /// </summary>
    public record RouteFilterFacetsDto
    {
        public List<DepartmentCategoryNameFacetDto> Pairs { get; set; } = new();
    }

    public record DepartmentCategoryNameFacetDto
    {
        public int DepartmentId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }
}
