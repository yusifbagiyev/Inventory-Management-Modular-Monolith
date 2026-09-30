using MediatR;

namespace SharedServices.Events
{
    /// <summary>
    /// State of a product at a point in time, as carried by product events. Image references are
    /// site-relative URLs (e.g. /images/products/1234/x.jpg), never bytes.
    /// </summary>
    public record ProductState
    {
        public int ProductId { get; init; }
        public int InventoryCode { get; init; }
        public string Model { get; init; } = string.Empty;
        public string Vendor { get; init; } = string.Empty;
        public int CategoryId { get; init; }
        public string CategoryName { get; init; } = string.Empty;
        public int DepartmentId { get; init; }
        public string DepartmentName { get; init; } = string.Empty;
        public string? Worker { get; init; }
        public string? Description { get; init; }
        public bool IsActive { get; init; }
        public bool IsWorking { get; init; }
        public bool IsNewItem { get; init; }
        public string? ImageUrl { get; init; }
    }

    /// <summary>
    /// Published inside the creating transaction. The route module writes the history row in the
    /// same transaction; notification handlers defer their work until after commit.
    /// </summary>
    public record ProductCreatedEvent(ProductState Product, DateTime CreatedAt) : INotification;

    /// <param name="Before">State before the update.</param>
    /// <param name="After">State after the update.</param>
    /// <param name="Changes">Human-readable change summary ("Model: A → B, ...").</param>
    /// <param name="NewImageUrl">Set only when the update uploaded a new image.</param>
    public record ProductUpdatedEvent(
        ProductState Before,
        ProductState After,
        string Changes,
        string? NewImageUrl,
        DateTime UpdatedAt) : INotification;

    public record ProductDeletedEvent(ProductState Product, string RemovedBy, DateTime DeletedAt) : INotification;
}
