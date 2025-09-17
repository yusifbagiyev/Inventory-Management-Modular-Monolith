using InventoryManagement.Web.Models.DTOs;

namespace InventoryManagement.Web.Services.Interfaces
{
    public interface ITokenManager
    {
        Task<bool> RefreshTokenAsync();
        Task<string?> GetValidTokenAsync();
    }
}