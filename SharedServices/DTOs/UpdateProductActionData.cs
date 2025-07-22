namespace SharedServices.DTOs
{
    public record UpdateProductActionData
    {
        public int ProductId { get; set; }
        public object UpdateData { get; set; } = null!;
    }
}
