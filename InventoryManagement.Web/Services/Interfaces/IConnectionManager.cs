namespace InventoryManagement.Web.Services.Interfaces
{
    public interface IConnectionManager
    {
        Task<string> GetSignalRTokenAsync();
    }
}