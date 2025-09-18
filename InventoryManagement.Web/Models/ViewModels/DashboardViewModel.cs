namespace InventoryManagement.Web.Models.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalProducts { get; set; }
        public int ActiveProducts { get; set; }
        public int TotalRoutes { get; set; }
        public int? PendingTransfers { get; set; }
        public int? CompletedTransfers { get; set; }
        public List<DepartmentStats> DepartmentStats { get; set; }=[];
        public List<RecentActivity> RecentActivities { get; set; } = [];
        public List<CategoryDistribution> CategoryDistributions { get; set; } = [];
        public TransferActivityData TransferActivityData { get; set; } = new();
    }

    public class DepartmentStats
    {
        public string DepartmentName { get; set; }=string.Empty;
        public int ProductCount { get; set; }
        public int ActiveWorkers { get; set; }
        public int PeriodTransfers { get; set; }
    }
    public class RecentActivity
    {
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }
    public class TransferActivityData
    {
        public List<string> Labels { get; set; } = [];
        public List<int> CompletedData { get; set; } = [];
        public List<int> PendingData { get; set; } = [];
    }
    public class CategoryDistribution
    {
        public string CategoryName { get; set; } = string.Empty;
        public int Count { get; set; }
        public string Color { get; set; } = string.Empty;
    }
}