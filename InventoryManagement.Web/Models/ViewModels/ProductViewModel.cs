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


        /// <summary>Name/value lines; rows without a name are dropped on save.</summary>
        public List<ProductSpecificationViewModel> Specifications { get; set; } = [];


        [Required]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }


        [Required]
        [Display(Name = "Department")]
        public int DepartmentId { get; set; }



        /// <summary>New images (create: in order, first is the cover; edit: added after the current ones).</summary>
        [Display(Name = "Images")]
        public List<IFormFile>? ImageFiles { get; set; }

        /// <summary>Edit: current images to remove, and the one to make the cover.</summary>
        public List<string>? RemoveImageUrls { get; set; }
        public string? CoverImageUrl { get; set; }

        /// <summary>The cover image (first of <see cref="ImageUrls"/>).</summary>
        public string? ImageUrl { get; set; }
        public List<string> ImageUrls { get; set; } = [];
        public string? CategoryName { get; set; }
        public string? DepartmentName { get; set; }

        [Display(Name = "Has Pending Approval")]
        public bool HasPendingApproval => PendingRequestId.HasValue;
        /// <summary>The request waiting for approval that changes this product (update, delete, transfer).</summary>
        public int? PendingRequestId { get; set; }

        //For Dropdowns
        public List<SelectListItem>? Categories { get; set; }
        public List<SelectListItem>? Departments { get; set; }


        /// <summary>Images are served by this host, so the stored site-relative URL is used as-is.</summary>
        public string? FullImageUrl => string.IsNullOrEmpty(ImageUrl) ? null : ImageUrl;

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        /// <summary>Set for a deleted product (Deleted products page).</summary>
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }
}