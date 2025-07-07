using System.ComponentModel.DataAnnotations;

namespace IdentityService.Application.DTOs
{
    public record UpdateUserDto
    {
        [Required]
        public int Id { get; init; }

        [Required]
        public string Username { get; init; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; init; } = string.Empty;

        [Required]
        public string FirstName { get; init; } = string.Empty;

        [Required]
        public string LastName { get; init; } = string.Empty;

        public bool? IsActive { get; init; }
    }

    public record ResetPasswordDto
    {
        [Required]
        [MinLength(6)]
        public string NewPassword { get; init; } = string.Empty;
    }

    public record AssignRoleDto
    {
        [Required]
        public string RoleName { get; init; } = string.Empty;
    }

    public record RemoveRoleDto
    {
        [Required]
        public string RoleName { get; init; } = string.Empty;
    }

    public record CheckPermissionDto
    {
        [Required]
        public string Permission { get; init; } = string.Empty;
    }

    public record LogoutDto
    {
        [Required]
        public string RefreshToken { get; init; } = string.Empty;
    }

    public record ChangePasswordDto
    {
        [Required]
        public string CurrentPassword { get; init; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string NewPassword { get; init; } = string.Empty;
    }

    public record UserStatusDto
    {
        public int Id { get; init; }
        public string Username { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? LastLoginAt { get; init; }
        public List<string> Roles { get; init; } = new();
    }

    public record PermissionDto
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
    }

    public record RoleDto
    {
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public List<string> Permissions { get; init; } = new();
    }
    public record GrantPermissionDto
    {
        [Required]
        public string PermissionName { get; init; } = string.Empty;
    }

    public record RevokePermissionDto
    {
        [Required]
        public string PermissionName { get; init; } = string.Empty;
    }
}