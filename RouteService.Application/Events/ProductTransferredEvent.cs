namespace RouteService.Application.Events
{
    public record ProductTransferredEvent
    {
        public int ProductId { get; set; }
        public int ToDepartmentId { get; set; }
        public string? ToWorker { get; set; }
        public byte[]? ImageData { get; set; }
        public string? ImageFileName { get; set; }
        public DateTime TransferredAt { get; set; }
    }
}