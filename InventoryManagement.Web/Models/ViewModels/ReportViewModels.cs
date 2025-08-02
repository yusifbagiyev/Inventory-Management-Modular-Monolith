namespace InventoryManagement.Web.Models.ViewModels
{
    public record InventoryReportViewModel
    {
        public int TotalProducts { get; set; }
        public int WorkingItems { get; set; }
        public int NotWorkingItems { get; set; }
        public int NewItems { get; set; }
        public List<DepartmentReport> DepartmentReports { get; set; } = new();
        public List<CategoryReport> CategoryReports { get; set; } = new();
    }

    public record DepartmentReport
    {
        public string DepartmentName { get; set; } = string.Empty;
        public int TotalItems { get; set; }
        public int WorkingItems { get; set; }
        public int NotWorkingItems { get; set; }
        public int UtilizationPercentage { get; set; }
    }

    public record CategoryReport
    {
        public string CategoryName { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public decimal Percentage { get; set; }
    }

    public record TransferHistoryViewModel
    {
        public List<RouteViewModel> Routes { get; set; } = new();
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? DepartmentId { get; set; }
        public int TotalTransfers { get; set; }
        public int CompletedTransfers { get; set; }
        public int PendingTransfers { get; set; }
        public Dictionary<string, int> TransfersByDepartment { get; set; } = new();
        public Dictionary<string, int> TransfersByMonth { get; set; } = new();
    }
}