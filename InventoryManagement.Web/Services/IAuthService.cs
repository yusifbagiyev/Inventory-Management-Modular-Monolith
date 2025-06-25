using InventoryManagement.Web.Models.DTOs;

namespace InventoryManagement.Web.Services
{
    public interface IAuthService
    {
        Task<TokenDto?> LoginAsync(string username, string password);
        Task<TokenDto?> RefreshTokenAsync(string accessToken, string refreshToken);
        Task<bool> LogoutAsync();
    }
}