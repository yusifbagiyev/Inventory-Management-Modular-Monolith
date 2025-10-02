namespace InventoryManagement.Web.Models.DTOs
{
    public record DepartmentDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? DepartmentHead { get; set; } = string.Empty;
        public string? Description { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
    public record CreateDepartmentDto
    {
        public string Name { get; set; } = string.Empty;
        public string? DepartmentHead { get; set; } = string.Empty;
        public string? Description { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
    public record UpdateDepartmentDto
    {
        public string Name { get; set; } = string.Empty;
        public string? DepartmentHead {  get; set; } = string.Empty;
        public string? Description { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}