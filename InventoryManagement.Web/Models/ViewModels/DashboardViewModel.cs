namespace InventoryManagement.Web.Models.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalProducts { get; set; }
        public int ActiveProducts { get; set; }
        public int? PendingTransfers { get; set; }
        public int? CompletedTransfers { get; set; }
        public List<DepartmentStats> DepartmentStats { get; set; }=[];
        public List<CategoryDistribution> CategoryDistributions { get; set; } = [];
        public TransferActivityData TransferActivityData { get; set; } = new();

        /// <summary>The same numbers for the preceding period of equal length (null for "all time").</summary>
        public int? PreviousTotalProducts { get; set; }
        public int? PreviousCompletedTransfers { get; set; }
        public int? PreviousPendingTransfers { get; set; }

        /// <summary>Period bounds as yyyy-MM-dd for links to the filtered lists (null for "all time").</summary>
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
        /// <summary>Each bucket's first and last day (yyyy-MM-dd), for links to the routes list.</summary>
        public List<string> BucketStarts { get; set; } = [];
        public List<string> BucketEnds { get; set; } = [];
    }
    public class CategoryDistribution
    {
        public string CategoryName { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}