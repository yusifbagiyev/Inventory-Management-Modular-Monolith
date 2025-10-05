namespace ApiGateway.Dto
{
    public record ThrottleResult
    {
        public bool IsAllowed { get; set; }
        public TimeSpan? RetryAfter { get; set; }
        public string? Reason { get; set; }
    }
}