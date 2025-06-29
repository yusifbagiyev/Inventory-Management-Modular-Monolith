namespace ProductService.Domain.Entities
{
    public class Department
    {
        public int Id { get; set; }
        public string Name { get; private set; } = string.Empty;
        public string? Description { get; private set; } = string.Empty;
        public bool IsActive { get; private set; } = true;
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }
        // Navigation property
        public ICollection<Product> Products { get; private set; } = [];
        // For EF Core
        protected Department() { }
        public Department(string name, string? description)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Department name cannot be empty", nameof(name));
            Name = name;
            Description = description??string.Empty;
            CreatedAt = DateTime.UtcNow;
        }
        public void Update(string name, string? description)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Department name cannot be empty", nameof(name));

            if(!string.IsNullOrWhiteSpace(description))
                Description=description;

            Name = name;
            UpdatedAt = DateTime.UtcNow;
        }
        public void Activate()
        {
            IsActive = true;
            UpdatedAt = DateTime.UtcNow;
        }
        public void Deactivate()
        {
            IsActive = false;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
