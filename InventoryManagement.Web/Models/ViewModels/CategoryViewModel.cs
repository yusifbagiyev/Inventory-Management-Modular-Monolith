using System.ComponentModel.DataAnnotations;

namespace InventoryManagement.Web.Models.ViewModels
{
    public class CategoryViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Category Name")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Description")]
        [MaxLength(500)]
        public string? Description { get; set; } = string.Empty;

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public int ProductCount { get; set; }
    }
}