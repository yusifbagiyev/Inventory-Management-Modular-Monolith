namespace RouteService.Application.DTOs
{
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