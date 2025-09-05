using InventoryManagement.Web.Models.DTOs;

namespace InventoryManagement.Web.Services.Interfaces
{
    public interface ITokenRefreshService
    {
        Task<TokenDto?> RefreshTokenIfNeededAsync();
        bool IsTokenExpiringSoon(string token);
        void UpdateTokensEverywhere(TokenDto tokenDto);
    }
}