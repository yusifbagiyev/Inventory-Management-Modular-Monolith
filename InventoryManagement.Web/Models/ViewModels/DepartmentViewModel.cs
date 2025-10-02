using System.ComponentModel.DataAnnotations;

namespace InventoryManagement.Web.Models.ViewModels
{
    public class DepartmentViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Department Name")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Description")]
        [MaxLength(500)]
        public string? Description { get; set; } = string.Empty;

        [Display(Name = "Department Head")]
        [MaxLength(100)]
        public string? DepartmentHead { get; set; } = string.Empty;

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public int ProductCount { get; set; }
        public int WorkerCount { get; set; }
    }
}