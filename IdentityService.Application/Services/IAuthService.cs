using IdentityService.Application.DTOs;

namespace IdentityService.Application.Services
{
    public interface IAuthService
    {
        /// <summary>Checks credentials for the cookie sign-in with lockout, throwing UnauthorizedAccessException on failure.</summary>
        Task<UserDto> ValidateCredentialsAsync(string username, string password);
        Task<TokenDto> LoginAsync(LoginDto dto);
        Task<TokenDto> RegisterAsync(RegisterDto dto);
        Task<TokenDto> RefreshTokenAsync(RefreshTokenDto dto);
        Task LogoutAsync(string refreshToken);

        // Users
        Task<UserDto?> GetUserAsync(int userId);
        /// <summary>A value that changes when the user's sessions must end, or null for an inactive or missing user.</summary>
        Task<string?> GetSessionStampAsync(int userId);
        Task<IEnumerable<UserDto>> GetAllUsersAsync();
        Task<bool> UpdateUserAsync(UpdateUserDto dto);
        Task<bool> DeleteUserAsync(int userId);
        Task<bool> ToggleUserStatusAsync(int userId);

        // Passwords
        Task<bool> ResetPasswordAsync(int userId, string newPassword);
        /// <summary>Error holds the first identity error when the change fails.</summary>
        Task<(bool Succeeded, string? Error)> ChangePasswordAsync(int userId, string currentPassword, string newPassword);

        // Roles
        Task<IEnumerable<string>> GetAllRolesAsync();
        Task<bool> AssignRoleAsync(int userId, string roleName);
        Task<bool> RemoveRoleAsync(int userId, string roleName);
        /// <summary>Replaces the user's roles with the given ones.</summary>
        Task<bool> SetRolesAsync(int userId, IEnumerable<string> roleNames);

        // Permissions
        Task<IReadOnlyList<PermissionDto>> GetAllPermissionsAsync();
        Task<bool> HasPermissionAsync(int userId, string permission);
        Task<bool> GrantPermissionToUserAsync(int userId, string permissionName, string grantedBy);
        Task<bool> RevokePermissionFromUserAsync(int userId, string permissionName);
        Task<List<PermissionDto>> GetUserDirectPermissionsAsync(int userId);

        // Everyone in a role holds its permissions on top of their own
        Task<IReadOnlyList<string>> GetRolePermissionsAsync(string roleName);
        Task<bool> SetRolePermissionAsync(string roleName, string permissionName, bool grant);

    }
}