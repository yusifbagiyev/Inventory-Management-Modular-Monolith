using System.ComponentModel.DataAnnotations;

namespace IdentityService.Application.DTOs
{
    public record LoginDto
    {
        [Required]
        [StringLength(SharedServices.Identity.PasswordRules.MaxUsernameLength)]
        public string Username { get; init; } = string.Empty;

        [Required]
        [StringLength(SharedServices.Identity.PasswordRules.MaxPasswordLength)]
        public string Password { get; init; } = string.Empty;
    }

    public record UserDto
    {
        public int Id { get; init; }
        public string Username { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? LastLoginAt { get; init; }
        public List<string> Roles { get; init; } = [];
        public List<string> Permissions { get; init; } = [];
    }

    public record RegisterDto
    {
        [Required]
        public string Username { get; init; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; init; } = string.Empty;

        [Required]
        [MinLength(SharedServices.Identity.PasswordRules.MinLength)]
        public string Password { get; init; } = string.Empty;

        [Required]
        public string FirstName { get; init; } = string.Empty;

        [Required]
        public string LastName { get; init; } = string.Empty;

        public string? SelectedRole { get; init; }

        /// <summary>False creates an account that cannot sign in until it is activated.</summary>
        public bool IsActive { get; init; } = true;
    }

    public record TokenDto
    {
        public string AccessToken { get; init; } = string.Empty;
        public string RefreshToken { get; init; } = string.Empty;
        public DateTime ExpiresAt { get; init; }
        public UserDto User { get; init; } = null!;
    }
    public record RefreshTokenDto
    {
        public string AccessToken { get; init; } = string.Empty;
        [Required]
        public string RefreshToken { get; init; } = string.Empty;
    }
}