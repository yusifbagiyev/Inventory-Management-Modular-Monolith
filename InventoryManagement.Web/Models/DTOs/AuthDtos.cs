using System.ComponentModel.DataAnnotations;

namespace InventoryManagement.Web.Models.DTOs
{
    public class LoginDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public record TokenDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public User User { get; set; } = null!;
        public bool? RememberMe { get; set; }
    }
    public record User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public List<string> Roles { get; set; } = [];
        public List<string> Permissions { get; set; } = [];
    }
    public class RefreshTokenDto
    {
        public string? AccessToken { get; init; } = string.Empty;
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }
}