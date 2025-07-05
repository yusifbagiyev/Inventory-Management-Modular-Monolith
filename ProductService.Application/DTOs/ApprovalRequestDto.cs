namespace ProductService.Application.DTOs
{
    public record ApprovalRequestDto
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public record CreateApprovalRequestDto
    {
        public string RequestType { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public int? EntityId { get; set; }
        public object ActionData { get; set; } = null!;
    }
}