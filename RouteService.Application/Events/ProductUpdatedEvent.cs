using RouteService.Domain.ValueObjects;

namespace RouteService.Application.Events
{
    public record ProductUpdatedEvent
    {
        public ExistingProduct Product { get; set; } = null!;
        public string? Changes { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }
}