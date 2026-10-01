using System.Security.Claims;
using IdentityService.Application.DTOs;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.Rendering;
using IdentityAuth = IdentityService.Application.Services.IAuthService;

namespace InventoryManagement.Web.Services
{
    /// <summary>User administration for the UI, backed in-process by the identity module.</summary>
    public class UserManagementService : IUserManagementService
    {
        private readonly IdentityAuth _auth;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<UserManagementService> _logger;

        public UserManagementService(IdentityAuth auth, IHttpContextAccessor httpContextAccessor, ILogger<UserManagementService> logger)
        {
            _auth = auth;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<List<UserListViewModel>> GetAllUsersAsync()
            => (await _auth.GetAllUsersAsync())
                .Select(u => new UserListViewModel
                {
                    Id = u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    FullName = $"{u.FirstName} {u.LastName}",
                    IsActive = u.IsActive,
                    Roles = u.Roles,
                    CreatedAt = u.CreatedAt,
                    LastLoginAt = u.LastLoginAt
                })
                .ToList();

        public async Task<EditUserViewModel?> GetUserByIdAsync(int id)
        {
            var user = await _auth.GetUserAsync(id);
            if (user == null)
                return null;

            var roles = await GetAllRolesAsync();
            return new EditUserViewModel
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                IsActive = user.IsActive,
                CurrentRoles = user.Roles,
                SelectedRoles = user.Roles,
                AvailableRoles = roles
                    .Select(r => new SelectListItem { Value = r, Text = r, Selected = user.Roles.Contains(r) })
                    .ToList()
            };
        }

        public async Task<UserProfileViewModel?> GetUserProfileAsync(int id)
        {
            var user = await _auth.GetUserAsync(id);
            if (user == null)
                return null;

            var descriptions = (await _auth.GetAllPermissionsAsync()).ToDictionary(p => p.Name, p => p);
            return new UserProfileViewModel
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                IsActive = user.IsActive,
                Roles = user.Roles,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                Permissions = user.Permissions.Select(p => new Permissions
                {
                    Name = p,
                    DisplayName = descriptions.TryGetValue(p, out var d) ? d.Description : p,
                    Category = descriptions.TryGetValue(p, out var c) ? c.Category : p.Split('.')[0],
                    Description = descriptions.TryGetValue(p, out var e) ? e.Description : string.Empty
                }).ToList()
            };
        }

        public async Task<bool> CreateUserAsync(CreateUserViewModel model)
        {
            try
            {
                await _auth.RegisterAsync(new RegisterDto
                {
                    Username = model.Username,
                    Email = model.Email,
                    Password = model.Password,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    SelectedRole = model.SelectedRole
                });
                return true;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Creating user {Username} failed: {Reason}", model.Username, ex.Message);
                return false;
            }
        }

        public async Task<bool> UpdateUserAsync(EditUserViewModel model)
        {
            var updated = await _auth.UpdateUserAsync(new UpdateUserDto
            {
                Id = model.Id,
                Username = model.Username,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                IsActive = model.IsActive
            });

            // Role failures used to be logged and then reported as success. Null: roles unchanged.
            if (!updated || model.SelectedRoles is not { Count: > 0 })
                return updated;
            return await _auth.SetRolesAsync(model.Id, model.SelectedRoles);
        }

        public Task<bool> DeleteUserAsync(int id) => _auth.DeleteUserAsync(id);

        public Task<bool> ToggleUserStatusAsync(int id) => _auth.ToggleUserStatusAsync(id);

        public Task<bool> ResetPasswordAsync(int userId, string newPassword) => _auth.ResetPasswordAsync(userId, newPassword);

        public async Task<(bool Success, string? Error)> ChangePasswordAsync(string currentPassword, string newPassword)
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
                return (false, "You are not signed in.");

            return await _auth.ChangePasswordAsync(userId, currentPassword, newPassword);
        }

        public async Task<List<string>> GetAllRolesAsync() => (await _auth.GetAllRolesAsync()).ToList();
    }
}
