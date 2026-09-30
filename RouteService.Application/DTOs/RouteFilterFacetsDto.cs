namespace RouteService.Application.DTOs
{
    /// <summary>
    /// The distinct (department, category-name) combinations across routes. The route list uses this
    /// to make its Department and Category filters cascade. Category is a name here because routes
    /// store only the category name on the product snapshot, not an id.
    /// </summary>
    public class RouteFilterFacetsDto
    {
        public List<DepartmentCategoryNameFacetDto> Pairs { get; set; } = new();
    }

    public class DepartmentCategoryNameFacetDto
    {
        /// <summary>The department name as written on the routes (not the department's current name).</summary>
        public string DepartmentName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
    }
}
