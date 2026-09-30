namespace InventoryManagement.Web.Models.ViewModels
{
    /// <summary>Input for the shared _Pagination partial (any paged list).</summary>
    public record PaginationViewModel(int PageNumber, int PageSize, int TotalCount)
    {
        public int FirstItem => TotalCount == 0 ? 0 : ((PageNumber - 1) * PageSize) + 1;
        public int LastItem => Math.Min(PageNumber * PageSize, TotalCount);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber * PageSize < TotalCount;
    }
}
