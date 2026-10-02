using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InventoryManagement.Web.Models.ViewModels
{
    public class ProductSpecificationViewModel
    {
        [StringLength(50)]
        public string? Name { get; set; }

        [StringLength(200)]
        public string? Value { get; set; }
    }

    public class ProductViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Inventory Code")]
        [Range(1,9999)]
        public int InventoryCode { get; set; }


        [Display(Name = "Model")]
        public string? Model { get; set; }


        [Display(Name = "Vendor")]
        public string? Vendor { get; set; }


        [Display(Name = "Worker")]
        public string? Worker { get; set; }



        [Display(Name = "Description")]
        public string? Description { get; set; }



        [Display(Name = "Is Working")]
        public bool IsWorking { get; set; } = true;


        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;


        [Display(Name = "Is New Item")]
        public bool IsNewItem { get; set; } = true;


        [Display(Name = "Color")]
        [StringLength(30)]
        public string? Color { get; set; }


        /// <summary>Rows without a name are dropped on save.</summary>
        public List<ProductSpecificationViewModel> Specifications { get; set; } = [];


        [Required]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }


        [Required]
        [Display(Name = "Department")]
        public int DepartmentId { get; set; }



        /// <summary>The first file is the cover on create, while on edit the files go after the current images.</summary>
        [Display(Name = "Images")]
        public List<IFormFile>? ImageFiles { get; set; }

        public List<string>? RemoveImageUrls { get; set; }
        public string? CoverImageUrl { get; set; }

        /// <summary>Always the first of ImageUrls.</summary>
        public string? ImageUrl { get; set; }
        public List<string> ImageUrls { get; set; } = [];
        public string? CategoryName { get; set; }
        public string? DepartmentName { get; set; }

        [Display(Name = "Has Pending Approval")]
        public bool HasPendingApproval => PendingRequestId.HasValue;
        /// <summary>A pending update, delete or transfer request for this product.</summary>
        public int? PendingRequestId { get; set; }

        public List<SelectListItem>? Categories { get; set; }
        public List<SelectListItem>? Departments { get; set; }


        /// <summary>Images are served by this host, so the stored relative URL works as is.</summary>
        public string? FullImageUrl => string.IsNullOrEmpty(ImageUrl) ? null : ImageUrl;

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }
}