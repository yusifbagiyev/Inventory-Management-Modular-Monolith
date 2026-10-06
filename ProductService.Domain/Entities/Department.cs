namespace ProductService.Domain.Entities
{
    public class Department
    {
        public int Id { get; set; }
        public string Name { get; private set; } = string.Empty;
        public string? DepartmentHead { get; private set; } = string.Empty;
        public string? Description { get; private set; } = string.Empty;
        public bool IsActive { get; private set; } = true;
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }
        public ICollection<Product> Products { get; private set; } = [];

        // For EF Core
        protected Department() { }
        public Department(string name, string? description,string? departmentHead, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Department name cannot be empty", nameof(name));
            Name = name.Trim();
            Description = description??string.Empty;
            DepartmentHead = departmentHead ?? string.Empty;
            IsActive = isActive;
            CreatedAt = DateTime.Now;
        }
        public void Update(string name, string? description,string? departmentHead, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Department name cannot be empty", nameof(name));

            Name = name.Trim();
            Description = string.IsNullOrWhiteSpace(description) ? Cleared(Description) : description;
            DepartmentHead = string.IsNullOrWhiteSpace(departmentHead) ? Cleared(DepartmentHead) : departmentHead;
            IsActive = isActive;
            UpdatedAt = DateTime.Now;
        }

        // An older row may hold null for no value, and clearing it again should not count as a change
        private static string? Cleared(string? current) => string.IsNullOrEmpty(current) ? current : string.Empty;
    }
}
