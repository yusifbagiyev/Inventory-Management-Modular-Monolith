namespace InventoryManagement.Web.Services.Interfaces
{
    public interface ISecureTokenProvider
    {
        Task<string?> GetTokenForSignalRAsync();
        Task<bool> ValidateCurrentSessionAsync();
    }
}