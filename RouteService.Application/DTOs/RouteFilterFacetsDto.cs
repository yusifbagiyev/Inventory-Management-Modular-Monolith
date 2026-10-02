namespace RouteService.Application.DTOs
{
    /// <summary>Department and category name pairs found on routes, so the list filters can cascade.</summary>
    public class RouteFilterFacetsDto
    {
        public List<DepartmentCategoryNameFacetDto> Pairs { get; set; } = new();
    }

    public class DepartmentCategoryNameFacetDto
    {
        /// <summary>The name stored on the routes, which may differ from the department's current name.</summary>
        public string DepartmentName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
    }
}
