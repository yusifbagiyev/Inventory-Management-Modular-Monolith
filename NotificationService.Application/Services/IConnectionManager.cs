namespace NotificationService.Application.Services
{
    public interface IConnectionManager
    {
        Task AddConnection(string userId, string connectionId);
        Task RemoveConnection(string userId, string connectionId);
        Task<IEnumerable<string>> GetConnections(string userId);
    }
}