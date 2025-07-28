namespace InventoryManagement.Web.Models.DTOs
{
    public class ProductDto
    {
        public int Id { get; set; }
        public int InventoryCode { get; set; }
        public string Model { get; set; } = string.Empty;
        public string Vendor { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Worker { get; set; } = string.Empty;
        public bool IsWorking { get; set; }
        public bool IsActive { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
    }

    public class BatchDeleteDto
    {
        public List<int> RouteIds { get; set; } = new();
    }

    public class BatchDeleteResultDto
    {
        public List<int> SuccessfulIds { get; set; } = new();
        public List<DeleteFailureDto> Failed { get; set; } = new();
        public int TotalProcessed { get; set; }
        public int TotalSuccessful { get; set; }
        public int TotalFailed { get; set; }
    }

    public class DeleteFailureDto
    {
        public int RouteId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}