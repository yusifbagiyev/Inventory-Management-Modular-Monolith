namespace NotificationService.Application.DTOs
{
    public record WhatsAppProductNotification
    {
        public int ProductId { get; set; }
        public int InventoryCode { get; set; }
        public string Model { get; set; } = string.Empty;
        public string Vendor { get; set; } = string.Empty;
        public string? CategoryName { get; set; }=string.Empty;
        public string? FromDepartmentName { get; set; }=string.Empty;
        public string ToDepartmentName { get; set; } = string.Empty;
        public string? FromWorker { get; set;} = string.Empty;
        public string? ToWorker { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsNewItem { get; set; }
        public bool IsWorking { get; set; } = true;
        public string? Notes { get; set; }=string.Empty;
        public string NotificationType { get; set; } = string.Empty;

        public string? ImageUrl { get; set; }
        public byte[]? ImageData { get; set; }
        public string? ImageFileName { get; set; }
    }
}