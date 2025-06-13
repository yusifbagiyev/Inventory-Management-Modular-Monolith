namespace RouteService.Application.DTOs
{
    public record RouteStatisticsDto
    {
        public int TotalRoutes { get; set; }
        public int CompletedRoutes { get; set; }
        public int PendingRoutes { get; set; }
        public Dictionary<string, int> RoutesByType { get; set; } = new();
        public Dictionary<string, int> RoutesByDepartment { get; set; } = new();
    }
}
