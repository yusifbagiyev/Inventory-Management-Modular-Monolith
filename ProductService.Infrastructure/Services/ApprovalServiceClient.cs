using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using ProductService.Application.DTOs;
using ProductService.Application.Features.Categories.Queries;
using ProductService.Application.Features.Departments.Queries;
using ProductService.Application.Interfaces;
using SharedServices.DTOs;
using SharedServices.Enum;

namespace ProductService.Infrastructure.Services
{
    public class ApprovalServiceClient : IApprovalService
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMediator _mediator;
        private readonly IConfiguration _configuration;

        public ApprovalServiceClient(
            HttpClient httpClient, 
            IHttpContextAccessor httpContextAccessor,
            IMediator mediator,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _httpContextAccessor = httpContextAccessor;
            _mediator = mediator;

            var baseUrl = configuration["Services:ApprovalService"] ?? "http://localhost:5000";
            _httpClient.BaseAddress = new Uri(baseUrl);
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
            if (actionData.ProductData is Dictionary<string, object> productData)
            {
                try
                {
                    // Extract IDs from the dictionary
                    var categoryId = (int)productData["categoryId"];
                    var departmentId = (int)productData["departmentId"];

                    // Get category and department names
                    var categoryName = await GetCategoryNameAsync(categoryId);
                    var departmentName = await GetDepartmentNameAsync(departmentId);

                    // Add the names to the existing dictionary
                    productData["CategoryName"] = categoryName;
                    productData["DepartmentName"] = departmentName;
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