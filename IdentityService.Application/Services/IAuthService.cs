using IdentityService.Application.DTOs;

namespace IdentityService.Application.Services
{
    public interface IAuthService
    {
        // Authentication methods
        /// <summary>
        /// Verifies username/password (with lockout) and records the login. Used by the UI's cookie
        /// sign-in; throws UnauthorizedAccessException on failure.
        /// </summary>
        Task<UserDto> ValidateCredentialsAsync(string username, string password);
        Task<TokenDto> LoginAsync(LoginDto dto);
        Task<TokenDto> RegisterAsync(RegisterDto dto);
        Task<TokenDto> RefreshTokenAsync(RefreshTokenDto dto);
        Task LogoutAsync(string refreshToken);

        // User management methods
        Task<UserDto?> GetUserAsync(int userId);
        Task<IEnumerable<UserDto>> GetAllUsersAsync();
        Task<bool> UpdateUserAsync(UpdateUserDto dto);
        Task<bool> DeleteUserAsync(int userId);
        Task<bool> ToggleUserStatusAsync(int userId);

        // Password management
        Task<bool> ResetPasswordAsync(int userId, string newPassword);
        /// <returns>Error is the first identity error description when the change fails.</returns>
        Task<(bool Succeeded, string? Error)> ChangePasswordAsync(int userId, string currentPassword, string newPassword);

        // Role management
        Task<IEnumerable<string>> GetAllRolesAsync();
        Task<bool> AssignRoleAsync(int userId, string roleName);
        Task<bool> RemoveRoleAsync(int userId, string roleName);
        /// <summary>Makes the user's roles exactly <paramref name="roleNames"/>.</summary>
        Task<bool> SetRolesAsync(int userId, IEnumerable<string> roleNames);

        // Permission management
        Task<IReadOnlyList<PermissionDto>> GetAllPermissionsAsync();
        Task<bool> HasPermissionAsync(int userId, string permission);
        Task<bool> GrantPermissionToUserAsync(int userId, string permissionName, string grantedBy);
        Task<bool> RevokePermissionFromUserAsync(int userId, string permissionName);
        Task<List<PermissionDto>> GetUserDirectPermissionsAsync(int userId);

        // Role permissions: everyone in the role holds them, on top of their own.
        Task<IReadOnlyList<string>> GetRolePermissionsAsync(string roleName);
        Task<bool> SetRolePermissionAsync(string roleName, string permissionName, bool grant);

    }
}