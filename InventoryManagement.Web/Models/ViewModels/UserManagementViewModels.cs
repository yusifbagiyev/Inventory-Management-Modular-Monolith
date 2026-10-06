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
        // The characters Identity accepts in a username, checked here so the message lands on the field
        [Required]
        [StringLength(50, MinimumLength = 3)]
        [RegularExpression(@"^[a-zA-Z0-9._@+\-]+$", ErrorMessage = "The username can contain only Latin letters, digits and the symbols . _ - @ +")]
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
        [StringLength(100, MinimumLength = SharedServices.Identity.PasswordRules.MinLength, ErrorMessage = SharedServices.Identity.PasswordRules.LengthMessage)]
        [RegularExpression(SharedServices.Identity.PasswordRules.RequiredCharacters, ErrorMessage = SharedServices.Identity.PasswordRules.LengthMessage)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [Compare("Password")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        public string SelectedRole { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
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
        /// <summary>Holds a single role, and null leaves the roles unchanged.</summary>
        public List<string>? SelectedRoles { get; set; } = new List<string>();
    }

    public record ResetPasswordViewModel
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = SharedServices.Identity.PasswordRules.MinLength, ErrorMessage = SharedServices.Identity.PasswordRules.LengthMessage)]
        [RegularExpression(SharedServices.Identity.PasswordRules.RequiredCharacters, ErrorMessage = SharedServices.Identity.PasswordRules.LengthMessage)]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [Compare("NewPassword")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }





    public record TogglePermissionViewModel
    {
        public string PermissionName { get; set; } = string.Empty;
        public bool IsGranting { get; set; }
    }
}