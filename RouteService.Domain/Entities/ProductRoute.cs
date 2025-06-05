using ProductService.Domain.Entities;

namespace RouteService.Domain.Entities
{
    public class ProductRoute
    {
        public int Id { get; private set; }
        public int InventoryCode { get; private set; }
        public int ProductId { get; private set; }
        public int FromDepartmentId { get; private set; }
        public int ToDepartmentId { get; private set; }

        public Department FromDepartment { get; private set; } = null!;
        public Department ToDepartment { get; private set; } = null!;
        public Product Product { get; private set; } = null!;

        public string? Category { get; private set; }
        public string? Vendor { get; private set; }
        public string? Model { get; private set; }

        public string? FromWorker { get; private set; }
        public string? ToWorker { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get;private set; }
        public string? ImagePath { get; private set; }
        public string? Description { get; private set; }
        public bool IsWorking { get; private set; }

        // For EF Core
        protected ProductRoute() { }

        public ProductRoute(int productId, int inventoryCode, int fromDepartmentId, int toDepartmentId, string? category, string? vendor, string? model, string? fromWorker, string? toWorker, string? imagePath, string? description)
        {
            if (inventoryCode <= 0)
                throw new ArgumentException("Inventory Code must be greater than zero", nameof(inventoryCode));
            if (productId <= 0)
                throw new ArgumentException("Product ID must be greater than zero", nameof(productId));
            if (fromDepartmentId <= 0)
                throw new ArgumentException("From Department ID must be greater than zero", nameof(fromDepartmentId));
            if (toDepartmentId <= 0)
                throw new ArgumentException("To Department ID must be greater than zero", nameof(toDepartmentId));

            InventoryCode = inventoryCode;
            ProductId = productId;
            FromDepartmentId = fromDepartmentId;
            ToDepartmentId = toDepartmentId;
            Category = category;
            Vendor = vendor;
            Model = model;
            FromWorker = fromWorker;
            ToWorker = toWorker;
            ImagePath = imagePath;
            Description = description;
            CreatedAt = DateTime.UtcNow;
        }
        public void Update(int toDepartmentId, string? category, string? vendor, string? model, string? toWorker, string? imagePath, string? description)
        {
            if (toDepartmentId <= 0)
                throw new ArgumentException("To Department ID must be greater than zero", nameof(toDepartmentId));

            ToDepartmentId = toDepartmentId;
            Category = category;
            Vendor = vendor;
            Model = model;
            ToWorker = toWorker;
            ImagePath = imagePath;
            Description = description;
            UpdatedAt = DateTime.UtcNow;
        }
        public void SetProductStatus(bool status)
        {
            IsWorking = status;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}