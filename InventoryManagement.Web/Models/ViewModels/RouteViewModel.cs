using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InventoryManagement.Web.Models.ViewModels
{
    public record RouteViewModel
    {
        public int Id { get; set; }
        public string RouteType { get; set; } = string.Empty;
        public string RouteTypeName { get; set; } = string.Empty;
        /// <summary>The type as an English text key for L[...] ("CodeChange" reads "Code change").</summary>
        public string RouteTypeLabel => RouteTypeName == "CodeChange" ? "Code change" : RouteTypeName;
        public int ProductId { get; set; }
        public int InventoryCode { get; set; }
        public string Model { get; set; } = string.Empty;
        public string Vendor { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int? FromDepartmentId { get; set; }
        public string? FromDepartmentName { get; set; }
        public int ToDepartmentId { get; set; }
        public string ToDepartmentName { get; set; } = string.Empty;
        public string? FromWorker { get; set; }
        public string? ToWorker { get; set; }
        /// <summary>The cover image (first of <see cref="ImageUrls"/>).</summary>
        public string? ImageUrl { get; set; }
        public List<string> ImageUrls { get; set; } = [];
        public string? Notes { get; set; }
        public bool IsCompleted { get; set; }
        /// <summary>Images are served by this host, so the stored site-relative URL is used as-is.</summary>
        public string? FullImageUrl => string.IsNullOrEmpty(ImageUrl) ? null : ImageUrl;
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public record TransferViewModel
    {
        [Required]
        [Display(Name = "Product")]
        public int ProductId { get; set; }

        [Required]
        [Display(Name = "To Department")]
        public int ToDepartmentId { get; set; }

        [Display(Name = "To Worker")]
        public string? ToWorker { get; set; }

        [Display(Name = "Images")]
        public List<IFormFile>? ImageFiles { get; set; }

        [Display(Name = "Notes")]
        [MaxLength(500)]
        public string? Notes { get; set; }

        // For dropdowns
        public List<SelectListItem>? Departments { get; set; }
    }

    public record PagedResultDto<T>
    {
        public IEnumerable<T> Items { get; set; } = [];
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
        public int? ActiveItems { get; set; }
        public int? InActiveItems { get; set; }
        public int? ItemsInWithProducts { get; set; }
    }

    public record UpdateRouteViewModel
    {
        /// <summary>Images added after the current ones; current ones to remove; the one to make the cover.</summary>
        public List<IFormFile>? ImageFiles { get; set; }
        public List<string>? RemoveImageUrls { get; set; }
        public string? CoverImageUrl { get; set; }
        public int ToDepartmentId { get; set; }
        public string? ToWorker { get; set; }
        public string? Notes { get; set; }
    }
}