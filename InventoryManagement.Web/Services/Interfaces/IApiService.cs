using InventoryManagement.Web.Models.DTOs;

namespace InventoryManagement.Web.Services.Interfaces
{
    public interface IApiService
    {
        Task<T?> GetAsync<T>(string endpoint);
        Task<ApiResponse<T>> PostAsync<T>(string endpoint, object? data);
        Task<ApiResponse<T>> PutAsync<T>(string endpoint, object? data);
        Task<ApiResponse<bool>> DeleteAsync(string endpoint);
        Task<ApiResponse<T>> PostFormAsync<T>(string endpoint, IFormCollection form, object? dataDto = null);
        Task<ApiResponse<TResponse>> PutFormAsync<TResponse>(string endpoint, IFormCollection form, object? dataDto = null);
    }
}