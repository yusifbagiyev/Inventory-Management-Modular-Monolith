namespace ProductService.Domain.Entities
{
    public class Product
    {
        public int Id { get; set; }
        public int InventoryCode { get; private set; }
        public string Model { get; private set; } = string.Empty;
        public string Vendor { get; private set; } = string.Empty;
        public string? Worker { get; private set; } = string.Empty;
        public string? ImageUrl { get; private set; } = string.Empty;
        public string? Description { get; private set; } = string.Empty;
        public bool IsWorking { get; private set; } = true;
        public bool IsActive { get; private set; } = true;
        public int CategoryId { get; private set; }
        public int DepartmentId { get; private set; }
        public bool IsNewItem { get; private set; }
        public Category? Category { get; private set; }
        public Department? Department { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }

        // For EF Core
        protected Product() { }

        public Product(int inventoryCode, string? model, string? vendor, int categoryId, int departmentId, string? worker, string? imageUrl, string? description, bool isActive, bool isWorking, bool isNewItem = true)
        {
            if (inventoryCode <= 0)
                throw new ArgumentException("Inventory Code must be greater than zero", nameof(inventoryCode));
            if (categoryId <= 0)
                throw new ArgumentException("Category ID must be greater than zero", nameof(categoryId));
            if (departmentId <= 0)
                throw new ArgumentException("Department ID must be greater than zero", nameof(departmentId));

            InventoryCode = inventoryCode;
            CategoryId = categoryId;
            DepartmentId = departmentId;
            Model = model ?? "No Name";
            Vendor = vendor ?? "No Name";
            ImageUrl = imageUrl ?? string.Empty;
            Description = description ?? string.Empty;
            Worker = worker;
            IsActive = isActive;
            IsWorking = isWorking;
            IsNewItem = isNewItem;
            CreatedAt = DateTime.UtcNow;
        }

        public void Update(string? model, string? vendor, int categoryId, int departmentId,string? worker, string? imageUrl, string? description)
        {
            if(categoryId <= 0)
                throw new ArgumentException("Category ID must be greater than zero", nameof(categoryId));
            if(departmentId <= 0)
                throw new ArgumentException("Department ID must be greater than zero", nameof(departmentId));

            Model = model ?? "No Name";
            Vendor = vendor ?? "No Name";
            Worker = worker;
            CategoryId = categoryId;
            DepartmentId = departmentId;
            ImageUrl = imageUrl ?? string.Empty;
            Description = description ?? string.Empty;
            UpdatedAt = DateTime.UtcNow;
        }
        public void UpdateAfterRouting(int departmentId, string? worker)
        {
            if (departmentId <= 0)
                throw new ArgumentException("Department ID must be greater than zero", nameof(departmentId));
            DepartmentId = departmentId;
            Worker = worker;
            UpdatedAt = DateTime.UtcNow;
        }
        public void UpdateImage(string imageUrl)
        {
            ImageUrl = imageUrl;
            UpdatedAt = DateTime.UtcNow;
        }
        public void ChangeInventoryCode(int inventoryCode)
        {
            if (inventoryCode <= 0)
                throw new ArgumentException("Inventory Code must be greater than zero", nameof(inventoryCode));

            InventoryCode = inventoryCode;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetActiveStatus(bool isActive)
        {
            IsActive = isActive;
            UpdatedAt = DateTime.UtcNow;
        }
        public void SetWorkingStatus()
        {
            IsWorking = true;
            UpdatedAt = DateTime.UtcNow;
        }
        public void SetNotWorkingStatus()
        {
            IsWorking = false;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}