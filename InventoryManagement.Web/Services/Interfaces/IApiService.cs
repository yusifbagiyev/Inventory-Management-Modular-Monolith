using InventoryManagement.Web.Models.DTOs;

namespace InventoryManagement.Web.Services.Interfaces
{
    public interface IApiService
    {
        Task<T?> GetAsync<T>(string endpoint);
        Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(string endpoint, TRequest data);
        Task<ApiResponse<TResponse>> PutAsync<TRequest, TResponse>(string endpoint, TRequest data);
        Task<ApiResponse<bool>> DeleteAsync(string endpoint);
        Task<ApiResponse<T>> PostFormAsync<T>(string endpoint, IFormCollection form, object? dataDto = null);
        Task<ApiResponse<TResponse>> PutFormAsync<TResponse>(string endpoint, IFormCollection form, object? dataDto = null);
    }
}