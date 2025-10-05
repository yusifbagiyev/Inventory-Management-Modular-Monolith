namespace ApiGateway.Dto
{
    public record ThrottleLimits
    {
        public int RequestsPerMinute { get; set; }
        public int BurstSize { get; set; }
    }
}