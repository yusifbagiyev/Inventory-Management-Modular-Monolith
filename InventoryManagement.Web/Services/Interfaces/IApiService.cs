namespace InventoryManagement.Web.Services.Interfaces
{
    public interface IApiService
    {
        Task<T?> GetAsync<T>(string endpoint);
        Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest data);
        Task<TResponse?> PutAsync<TRequest, TResponse>(string endpoint, TRequest data);
        Task<bool> DeleteAsync(string endpoint);
        Task<TResponse?> PostFormAsync<TResponse>(string endpoint, IFormCollection form,object? dataDto=null);
        Task<TResponse?> PutFormAsync<TResponse>(string endpoint, IFormCollection form, object? dataDto = null); // Add this
    }
}