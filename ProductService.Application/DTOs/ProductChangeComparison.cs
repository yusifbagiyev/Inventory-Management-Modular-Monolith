namespace ProductService.Application.DTOs
{
    public record ProductChangeComparison
    {
        public Dictionary<string, ChangeDetail> Changes { get; set; } = new();
        public bool HasChanges { get; set; }
        public string Summary { get; set; } = "";
    }
    public record ChangeDetail
    {
        public string Old { get; set; } = "";
        public string New { get; set; } = "";
    }
}