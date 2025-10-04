using InventoryManagement.Web.Models.DTOs;

namespace InventoryManagement.Web.Services.Interfaces
{
    public interface IAuthService
    {
        Task<TokenDto?> LoginAsync(string username, string password,bool rememberMe);
        Task<TokenDto?> RefreshTokenAsync(string refreshToken,string accessToken);
    }
}