using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using RouteService.Application.DTOs;
using RouteService.Application.Interfaces;

namespace RouteService.Infrastructure.Services
{
    public class ProductServiceClient : IProductServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public ProductServiceClient(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _baseUrl = configuration["ProductService:BaseUrl"] ?? "http://localhost:5090";
        }

        public async Task<ProductInfoDto?> GetProductByIdAsync(int productId, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/products/{productId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<ProductInfoDto>(cancellationToken: cancellationToken);
        }

        public async Task<DepartmentDto?> GetDepartmentByIdAsync(int departmentId, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/departments/{departmentId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<DepartmentDto>(cancellationToken: cancellationToken);
        }

        public async Task<bool> UpdateProductInfoAfterRouting(int productId, int departmentId, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PatchAsync(
                $"{_baseUrl}/api/products/{productId}/department",
                JsonContent.Create(new { DepartmentId = departmentId }),
                cancellationToken);

            return response.IsSuccessStatusCode;
        }

        public async Task<bool> UpdateProductStatusAsync(int productId, bool isActive, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PatchAsync(
                $"{_baseUrl}/api/products/{productId}/status",
                JsonContent.Create(new { IsActive = isActive }),
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
    }
}
