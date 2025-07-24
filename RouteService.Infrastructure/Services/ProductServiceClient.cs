using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using RouteService.Application.DTOs;
using RouteService.Application.Interfaces;

namespace RouteService.Infrastructure.Services
{
    public class ProductServiceClient : IProductServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public ProductServiceClient(HttpClient httpClient, IConfiguration configuration,IHttpContextAccessor httpContextAccessor)
        {
            _httpClient = httpClient;
            _baseUrl = configuration["ProductService:BaseUrl"] ?? "http://localhost:5001";
            _httpContextAccessor = httpContextAccessor;
        }

        private void AddAuthorizationHeader()
        {
            // Get the authorization header from the current request
            var authHeader = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", authHeader.Replace("Bearer ", ""));
            }
        }   
        public async Task<ProductInfoDto?> GetProductByIdAsync(int productId, CancellationToken cancellationToken = default)
        {
            AddAuthorizationHeader();

            var response = await _httpClient.GetAsync($"{_baseUrl}/api/products/{productId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<ProductInfoDto>(cancellationToken: cancellationToken);
        }
        public async Task<DepartmentDto?> GetDepartmentByIdAsync(int departmentId, CancellationToken cancellationToken = default)
        {
            AddAuthorizationHeader();

            var response = await _httpClient.GetAsync($"{_baseUrl}/api/departments/{departmentId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<DepartmentDto>(cancellationToken: cancellationToken);
        }
    }
}