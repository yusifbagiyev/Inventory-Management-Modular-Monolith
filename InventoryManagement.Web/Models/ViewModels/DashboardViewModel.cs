namespace InventoryManagement.Web.Models.ViewModels
{
    public class DashboardViewModel
    {
        /// <summary>Product figures are the current state and ignore the period.</summary>
        public int TotalProducts { get; set; }
        public int ActiveProducts { get; set; }
        public int InactiveProducts => TotalProducts - ActiveProducts;
        public int NotWorking { get; set; }
        /// <summary>Up to two not-working products for the tile's second line.</summary>
        public List<string> NotWorkingNames { get; set; } = [];
        public int? CompletedTransfers { get; set; }
        /// <summary>All open transfers regardless of the period.</summary>
        public int PendingTransfers { get; set; }
        public int? OldestPendingDays { get; set; }
        public List<DepartmentStats> DepartmentStats { get; set; }=[];
        public List<CategoryDistribution> CategoryDistributions { get; set; } = [];
        public TransferActivityData TransferActivityData { get; set; } = new();

        /// <summary>Period bounds for links to the filtered lists, null for all time.</summary>
        public string? PeriodStart { get; set; }
        public string? PeriodEnd { get; set; }
    }

    public class DepartmentStats
    {
        public string DepartmentName { get; set; }=string.Empty;
        public int ProductCount { get; set; }
        public int ActiveWorkers { get; set; }
        public int PeriodTransfers { get; set; }
    }
    public class TransferActivityData
    {
        public List<string> Labels { get; set; } = [];
        public List<int> CompletedData { get; set; } = [];
        public List<int> PendingData { get; set; } = [];
        /// <summary>First and last day of each bucket, used for links to the routes list.</summary>
        public List<string> BucketStarts { get; set; } = [];
        public List<string> BucketEnds { get; set; } = [];
    }
    public class CategoryDistribution
    {
        public string CategoryName { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}