namespace RouteService.Application.DTOs
{
    /// <summary>What a route delete request shows the approver about the route.</summary>
    public record DeleteRouteActionDataWithRequest
    {
        public int RouteId { get; set; }
        public string RouteType { get; set; } = "";
        public string ProductInfo { get; set; } = "";
        public string FromLocation { get; set; } = "";
        public string ToLocation { get; set; } = "";
        public DateTime CreatedDate { get; set; }
        public bool IsCompleted { get; set; }
    }
}