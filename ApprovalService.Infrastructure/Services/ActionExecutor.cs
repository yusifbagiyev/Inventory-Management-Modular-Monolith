using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ApprovalService.Application.DTOs;
using ApprovalService.Application.Interfaces;
using ApprovalService.Domain.Entities;
using ApprovalService.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ApprovalService.Infrastructure.Services
{
    public class ActionExecutor:IActionExecutor
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public ActionExecutor(HttpClient httpClient,IConfiguration configuration)
        {
            _httpClient= httpClient;
            _configuration= configuration;
        }

        public async Task<bool> ExecuteAsync(string requestType, string actionData, CancellationToken cancellationToken = default)
        {
            try
            {
                AddAuthorizationHeader();

                return requestType switch
                {
                    RequestType.CreateProduct => await ExecuteCreateProduct(actionData, cancellationToken),
                    RequestType.UpdateProduct => await ExecuteUpdateProduct(actionData, cancellationToken),
                    RequestType.DeleteProduct => await ExecuteDeleteProduct(actionData, cancellationToken),
                    RequestType.TransferProduct => await ExecuteTransferProduct(actionData, cancellationToken),
                    RequestType.DeleteRoute => await ExecuteDeleteRoute(actionData, cancellationToken),
                    _ => false
                };

            }
            catch
            {
                return false;
            }
        }

        private void AddAuthorizationHeader()
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]!);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.Role, "Admin")
        }),
                Expires = DateTime.UtcNow.AddMinutes(5),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"]
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", tokenHandler.WriteToken(token));
        }


        private async Task<bool> ExecuteCreateProduct(string actionData, CancellationToken cancellationToken)
        {
            try
            {
                var data = JsonSerializer.Deserialize<CreateProductActionData>(actionData);
                if (data?.ProductData == null)
                    return false;

                // Convert JsonElement to a simple object without IFormFile
                var productJson = data.ProductData.ToString();
                var jsonDoc = JsonDocument.Parse(productJson);
                var root = jsonDoc.RootElement;

                // Create a simplified product object
                var productData = new
                {
                    InventoryCode = root.TryGetProperty("inventoryCode", out var inv) ? inv.GetInt32() : 0,
                    Model = root.TryGetProperty("model", out var model) ? model.GetString() : "",
                    Vendor = root.TryGetProperty("vendor", out var vendor) ? vendor.GetString() : "",
                    Worker = root.TryGetProperty("worker", out var worker) ? worker.GetString() : "",
                    Description = root.TryGetProperty("description", out var desc) ? desc.GetString() : "",
                    IsWorking = root.TryGetProperty("isWorking", out var working) ? working.GetBoolean() : true,
                    IsActive = root.TryGetProperty("isActive", out var active) ? active.GetBoolean() : true,
                    IsNewItem = root.TryGetProperty("isNewItem", out var newItem) ? newItem.GetBoolean() : true,
                    CategoryId = root.TryGetProperty("categoryId", out var cat) ? cat.GetInt32() : 0,
                    DepartmentId = root.TryGetProperty("departmentId", out var dept) ? dept.GetInt32() : 0
                };

                var json = JsonSerializer.Serialize(productData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{_configuration["Services:ProductService"]}/api/products/approved",
                    content,
                    cancellationToken);

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> ExecuteUpdateProduct(string actionData, CancellationToken cancellationToken)
        {
            var data = JsonSerializer.Deserialize<UpdateProductActionData>(actionData);
            if (data != null)
            {
                var response = await _httpClient.PutAsync(
                $"{_configuration["Services:ProductService"]}/api/products/{data.ProductId}/approved",
                new StringContent(JsonSerializer.Serialize(data.UpdateData), Encoding.UTF8, "application/json"),
                cancellationToken);
                return response.IsSuccessStatusCode;
            }
            return false;
        }

        private async Task<bool> ExecuteDeleteProduct(string actionData, CancellationToken cancellationToken)
        {
            var data = JsonSerializer.Deserialize<DeleteProductActionData>(actionData);
            if (data != null)
            {
                var response = await _httpClient.DeleteAsync(
                $"{_configuration["Services:ProductService"]}/api/products/{data.ProductId}/approved",
                cancellationToken);
                return response.IsSuccessStatusCode;
            }
            return false;
        }

        private async Task<bool> ExecuteTransferProduct(string actionData, CancellationToken cancellationToken)
        {
            var response = await _httpClient.PostAsync(
                $"{_configuration["Services:RouteService"]}/api/inventoryroutes/transfer/approved",
                new StringContent(actionData, Encoding.UTF8, "application/json"),
                cancellationToken);
            return response.IsSuccessStatusCode;
        }

        private async Task<bool> ExecuteDeleteRoute(string actionData, CancellationToken cancellationToken)
        {
            var data = JsonSerializer.Deserialize<DeleteRouteActionData>(actionData);
            if (data != null)
            {
                var response = await _httpClient.DeleteAsync(
                $"{_configuration["Services:RouteService"]}/api/inventoryroutes/{data.RouteId}/approved",
                cancellationToken);
                return response.IsSuccessStatusCode;
            }
            return false;
        }
    }
}