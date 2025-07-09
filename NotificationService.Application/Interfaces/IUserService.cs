using NotificationService.Application.DTOs;

namespace NotificationService.Application.Interfaces
{
    public interface IUserService
    {
        Task<List<UserDto>> GetUsersAsync(string? role = null);
        Task<UserDto?> GetUserAsync(int userId);
        Task<List<int>> GetUserIdsByRoleAsync(string role, CancellationToken cancellationToken = default);
    }
}