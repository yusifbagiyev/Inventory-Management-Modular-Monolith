using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace InventoryManagement.Web.Models.ViewModels
{
    public record UserListViewModel
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }

    public record CreateUserViewModel
    {
        [Required]
        [StringLength(50, MinimumLength = 3)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [Compare("Password")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        public string SelectedRole { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        [StringLength(500)]
        public string Notes { get; set; } = string.Empty;

        public List<SelectListItem> Roles { get; set; } = new List<SelectListItem>();
    }

    public record EditUserViewModel
    {
        public int Id { get; set; }

        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string LastName { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public List<string> CurrentRoles { get; set; } = new List<string>();
        public List<string> SelectedRoles { get; set; } = new List<string>();
        public List<SelectListItem> AvailableRoles { get; set; } = new List<SelectListItem>();
    }

    public record ResetPasswordViewModel
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [Compare("NewPassword")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public record PermissionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool IsAssigned { get; set; }
    }

    public record ManagePermissionsViewModel
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public List<PermissionViewModel> Permissions { get; set; } = new List<PermissionViewModel>();
    }

    public record UserDetailsViewModel
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName => $"{FirstName} {LastName}";
        public bool IsActive { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public int LoginCount { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public record GrantPermissionViewModel
    {
        public string PermissionName { get; set; } = string.Empty;
    }

    public record RevokePermissionViewModel
    {
        public string PermissionName { get; set; } = string.Empty;
    }
    public record TogglePermissionViewModel
    {
        public string PermissionName { get; set; } = string.Empty;
        public bool IsGranting { get; set; }
    }
}