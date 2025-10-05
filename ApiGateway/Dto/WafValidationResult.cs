namespace ApiGateway.Dto
{
    public record WafValidationResult
    {
        public bool IsValid { get; set; }
        public string? Reason { get; set; }
        public List<string>? BlockedBy { get; set; }
    }
}