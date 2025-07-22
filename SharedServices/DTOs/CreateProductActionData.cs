namespace SharedServices.DTOs
{
    public record CreateProductActionData
    {
        public object ProductData { get; set; } = null!;
    }
}