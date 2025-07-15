using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ApprovalService.Shared.DTOs;
using ApprovalService.Shared.Enum;
using MediatR;
using Microsoft.AspNetCore.Http;
using ProductService.Application.DTOs;
using ProductService.Application.Features.Categories.Queries;
using ProductService.Application.Features.Departments.Queries;
using ProductService.Application.Interfaces;

namespace ProductService.Infrastructure.Services
{
    public class ApprovalServiceClient : IApprovalService
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMediator _mediator;

        public ApprovalServiceClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor,IMediator mediator)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("http://localhost:5000"); // Via API Gateway
            _httpContextAccessor = httpContextAccessor;
            _mediator = mediator;
        }

        public async Task<ApprovalRequestDto> CreateApprovalRequestAsync(CreateApprovalRequestDto dto, int userId, string userName)
        {
            var authHeader = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", authHeader.Replace("Bearer ", ""));
            }

            // If it's a product creation request, enrich with category and department names
            if (dto.RequestType == RequestType.CreateProduct && dto.ActionData is CreateProductActionData actionData)
            {
                await EnrichProductDataWithNames(actionData);
            }

            var content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/api/approvalrequests", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApprovalRequestDto>(responseContent);
            return result ?? throw new InvalidOperationException("Failed to create approval request");
        }

        private async Task EnrichProductDataWithNames(CreateProductActionData actionData)
        {
            if (actionData.ProductData is CreateProductDto productDto)
            {
                try
                {
                    // Get category and department names
                    var categoryName = await GetCategoryNameAsync(productDto.CategoryId);
                    var departmentName = await GetDepartmentNameAsync(productDto.DepartmentId);

                    // Create enriched product data
                    var enrichedData = new
                    {
                        productDto.InventoryCode,
                        productDto.Model,
                        productDto.Vendor,
                        productDto.Worker,
                        productDto.Description,
                        productDto.IsWorking,
                        productDto.IsActive,
                        productDto.IsNewItem,
                        productDto.CategoryId,
                        productDto.DepartmentId,
                        CategoryName = categoryName,
                        DepartmentName = departmentName
                    };

                    // Replace the ProductData with enriched data
                    actionData.ProductData = enrichedData;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to enrich product data: {ex.Message}");
                }
            }
        }

        private async Task<string> GetCategoryNameAsync(int categoryId)
        {
            try
            {
                var category = await _mediator.Send(new GetCategoryByIdQuery(categoryId));
                return category?.Name ?? $"Category #{categoryId}";
            }
            catch
            {
                return $"Category #{categoryId}";
            }
        }

        private async Task<string> GetDepartmentNameAsync(int departmentId)
        {
            try
            {
                var department = await _mediator.Send(new GetDepartmentByIdQuery(departmentId));
                return department?.Name ?? $"Department #{departmentId}";
            }
            catch
            {
                return $"Department #{departmentId}";
            }
        }
    }
}