namespace NotificationService.Application.Interfaces
{
    public interface IUserService
    {
        Task<List<UserDto>> GetUsersAsync(string? role = null);
        Task<UserDto?> GetUserAsync(int userId);
    }
    public record UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
    }
}