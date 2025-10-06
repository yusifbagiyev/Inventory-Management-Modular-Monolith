using InventoryManagement.Web.Models.ViewModels;

namespace InventoryManagement.Web.Services.Interfaces
{
    public interface IUserManagementService
    {
        Task<List<UserListViewModel>> GetAllUsersAsync();
        Task<EditUserViewModel> GetUserByIdAsync(int id);
        Task<UserProfileViewModel> GetUserProfileAsync(int id);
        Task<bool> CreateUserAsync(CreateUserViewModel model);
        Task<bool> UpdateUserAsync(EditUserViewModel model);
        Task<bool> DeleteUserAsync(int id);
        Task<bool> ToggleUserStatusAsync(int id);
        Task<bool> ResetPasswordAsync(int userId, string newPassword);
        Task<List<string>> GetAllRolesAsync();
    }
}