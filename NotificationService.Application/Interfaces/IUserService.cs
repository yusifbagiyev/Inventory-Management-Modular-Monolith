namespace NotificationService.Application.Interfaces
{
    public interface IUserService
    {
        Task<IEnumerable<int>> GetUserIdsByRoleAsync(string role, CancellationToken cancellationToken = default);
    }
}