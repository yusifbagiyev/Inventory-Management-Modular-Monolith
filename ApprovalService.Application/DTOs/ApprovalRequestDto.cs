namespace ApprovalService.Application.DTOs
{
    public record ApprovalRequestDto
    {
        public int Id { get; set; }
        public string RequestType { get; set; } = string.Empty;
        public string EntityType {  get; set; } = string.Empty;
        public int? EntityId { get; set; }
        public string ActionData {  get; set; } = string.Empty;
        public int RequestedById { get; set; }
        public string RequestedByName { get; set; } = string.Empty;
        public int? ApprovedById {  get; set; }
        public string? ApprovedByName { get;set; }
        public string Status { get; set; } = string.Empty;
        public string? RejectionReason { get; set; }
        public DateTime CreatedAt {  get; set; }
        public DateTime? ProcessedAt { get; set; }
        public DateTime? ExecutedAt { get; set; }
    }
    public record ApproveRequestDto
    {
        public int RequestId { get; set; }
    }
    public record RejectRequestDto
    {
        public int RequestId { get; set; }
        public string Reason {  get; set; } = string.Empty;
    }
    public record PagedResultDto<T>
    {
        public IEnumerable<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }
}