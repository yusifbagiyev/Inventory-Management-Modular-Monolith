using InventoryManagement.Web.Models.ViewModels;

namespace InventoryManagement.Web.Services.Interfaces
{
    public interface IUserManagementService
    {
        Task<List<UserListViewModel>> GetAllUsersAsync();
        Task<EditUserViewModel?> GetUserByIdAsync(int id);
        Task<UserProfileViewModel?> GetUserProfileAsync(int id);
        // Error holds the reason when the identity module refuses or fails a change
        Task<(bool Success, string? Error)> CreateUserAsync(CreateUserViewModel model);
        Task<(bool Success, string? Error)> UpdateUserAsync(EditUserViewModel model);
        Task<(bool Success, string? Error)> DeleteUserAsync(int id);
        Task<(bool Success, string? Error)> ToggleUserStatusAsync(int id);
        Task<(bool Success, string? Error)> ResetPasswordAsync(int userId, string newPassword);
        /// <summary>Changes the signed-in user's own password after checking the current one.</summary>
        Task<(bool Success, string? Error)> ChangePasswordAsync(string currentPassword, string newPassword);
        Task<List<string>> GetAllRolesAsync();
    }
}