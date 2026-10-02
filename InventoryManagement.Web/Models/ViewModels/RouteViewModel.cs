using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InventoryManagement.Web.Models.ViewModels
{
    public record RouteViewModel
    {
        public int Id { get; set; }
        public string RouteType { get; set; } = string.Empty;
        public string RouteTypeName { get; set; } = string.Empty;
        /// <summary>English text key for the route type.</summary>
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
        /// <summary>Always the first of ImageUrls.</summary>
        public string? ImageUrl { get; set; }
        public List<string> ImageUrls { get; set; } = [];
        public string? Notes { get; set; }
        public bool IsCompleted { get; set; }
        /// <summary>Images are served by this host, so the stored relative URL works as is.</summary>
        public string? FullImageUrl => string.IsNullOrEmpty(ImageUrl) ? null : ImageUrl;
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        /// <summary>Queued, Sent or Failed, null when no message was sent.</summary>
        public string? WhatsAppStatus { get; set; }
        public string? WhatsAppError { get; set; }
        public DateTime? WhatsAppAt { get; set; }
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
        /// <summary>New files go after the current images.</summary>
        public List<IFormFile>? ImageFiles { get; set; }
        public List<string>? RemoveImageUrls { get; set; }
        public string? CoverImageUrl { get; set; }
        public int ToDepartmentId { get; set; }
        public string? ToWorker { get; set; }
        public string? Notes { get; set; }
    }
}