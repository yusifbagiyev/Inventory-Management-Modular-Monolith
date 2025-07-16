using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ApprovalService.Application.Interfaces;
using ApprovalService.Shared.DTOs;
using ApprovalService.Shared.Enum;
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
                var jsonDoc = JsonDocument.Parse(actionData);
                var root = jsonDoc.RootElement;

                // Check if it's wrapped in ActionData structure
                JsonElement productElement;
                if (root.TryGetProperty("ProductData", out var productDataProp))
                {
                    productElement = productDataProp;
                }
                else
                {
                    productElement = root;
                }

                // Create a properly formatted object for the API
                var categoryName = GetStringProperty(productElement, "categoryName", "CategoryName");
                var departmentName = GetStringProperty(productElement, "departmentName", "DepartmentName");

                var productData = new
                {
                    inventoryCode = GetIntProperty(productElement, "inventoryCode", "InventoryCode"),
                    model = GetStringProperty(productElement, "model", "Model"),
                    vendor = GetStringProperty(productElement, "vendor", "Vendor"),
                    worker = GetStringProperty(productElement, "worker", "Worker"),
                    description = GetStringProperty(productElement, "description", "Description"),
                    isWorking = GetBoolProperty(productElement, "isWorking", "IsWorking", true),
                    isActive = GetBoolProperty(productElement, "isActive", "IsActive", true),
                    isNewItem = GetBoolProperty(productElement, "isNewItem", "IsNewItem", true),
                    categoryId = GetIntProperty(productElement, "categoryId", "CategoryId"),
                    departmentId = GetIntProperty(productElement, "departmentId", "DepartmentId"),
                    categoryName,
                    departmentName
                };

                var json = JsonSerializer.Serialize(productData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{_configuration["Services:ProductService"]}/api/products/approved",
                    content,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                }

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> ExecuteUpdateProduct(string actionData, CancellationToken cancellationToken)
        {
            try
            {
                var jsonDoc = JsonDocument.Parse(actionData);
                var root = jsonDoc.RootElement;

                //Extract ProductId and InventoryCode
                var productId = root.GetProperty("ProductId").GetInt32();
                
                var content=new StringContent(actionData, Encoding.UTF8, "application/json");

                var response=await _httpClient.PutAsync(
                    $"{_configuration["Services:ProductService"]}/api/products/{productId}/approved",
                    new StringContent(actionData, Encoding.UTF8, "application/json"), cancellationToken);

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> ExecuteDeleteProduct(string actionData, CancellationToken cancellationToken)
        {
            try
            {
                var jsonDoc=JsonDocument.Parse(actionData);
                var root=jsonDoc.RootElement;

                var productId=root.GetProperty("ProductId").GetInt32();

                var response=await _httpClient.DeleteAsync(
                    $"{_configuration["Services:ProductService"]}/api/products/{productId}/approved",
                    cancellationToken);

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
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

        private string GetStringProperty(JsonElement element, string camelCase, string pascalCase)
        {
            if (element.TryGetProperty(camelCase, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString() ?? "";
            if (element.TryGetProperty(pascalCase, out var propPascal) && propPascal.ValueKind == JsonValueKind.String)
                return propPascal.GetString() ?? "";
            return "";
        }
        private bool GetBoolProperty(JsonElement element, string camelCase, string pascalCase, bool defaultValue)
        {
            if (element.TryGetProperty(camelCase, out var prop) && prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False)
                return prop.GetBoolean();
            if (element.TryGetProperty(pascalCase, out var propPascal) && propPascal.ValueKind == JsonValueKind.True || propPascal.ValueKind == JsonValueKind.False)
                return propPascal.GetBoolean();
            return defaultValue;
        }
        private int GetIntProperty(JsonElement element, string camelCase, string pascalCase)
        {
            if (element.TryGetProperty(camelCase, out var prop) && prop.ValueKind == JsonValueKind.Number)
                return prop.GetInt32();
            if (element.TryGetProperty(pascalCase, out var propPascal) && propPascal.ValueKind == JsonValueKind.Number)
                return propPascal.GetInt32();
            return 0;
        }
    }
}