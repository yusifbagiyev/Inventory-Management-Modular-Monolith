namespace ProductService.Application.DTOs
{
    public record ApprovalRequestDto
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}