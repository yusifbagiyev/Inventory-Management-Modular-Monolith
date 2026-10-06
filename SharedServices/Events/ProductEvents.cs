using MediatR;

namespace SharedServices.Events
{
    /// <summary>A product's state as carried by product events, with images as site-relative URLs, never bytes.</summary>
    public record ProductState
    {
        public int ProductId { get; init; }
        public int InventoryCode { get; init; }
        public string Model { get; init; } = string.Empty;
        public string Vendor { get; init; } = string.Empty;
        public string CategoryName { get; init; } = string.Empty;
        public int DepartmentId { get; init; }
        public string DepartmentName { get; init; } = string.Empty;
        public string? Worker { get; init; }
        public string? Description { get; init; }
        public bool IsWorking { get; init; }
        public bool IsNewItem { get; init; }
        public string? ImageUrl { get; init; }
    }

    // Published inside the creating transaction, so the route history row commits with the product
    public record ProductCreatedEvent(ProductState Product, DateTime CreatedAt) : INotification;

    // Changes is a readable summary for notes, and NewImageUrl is set only when an image was uploaded
    public record ProductUpdatedEvent(
        ProductState Before,
        ProductState After,
        string Changes,
        string? NewImageUrl) : INotification;

    public record ProductDeletedEvent(ProductState Product, string RemovedBy) : INotification;

    // Published inside the purging transaction, so the product's route history goes with it
    public record ProductPurgedEvent(int ProductId, int InventoryCode) : INotification;
}
