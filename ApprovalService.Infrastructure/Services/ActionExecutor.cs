using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ApprovalService.Application.Interfaces;
using ApprovalService.Shared.DTOs;
using ApprovalService.Shared.Enum;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace ApprovalService.Infrastructure.Services
{
    public class ActionExecutor:IActionExecutor
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ActionExecutor> _logger;
        private readonly IConfiguration _configuration;

        public ActionExecutor(HttpClient httpClient,IConfiguration configuration,ILogger<ActionExecutor> logger)
        {
            _httpClient= httpClient;
            _configuration= configuration;
            _logger= logger;
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

                //Check if ImageFile is present
                byte[]? imageData=null;
                string? imageFileName = null;

                if(productElement.TryGetProperty("imageData",out var imageDataProp) &&
                    imageDataProp.ValueKind == JsonValueKind.String)
                {
                    imageData= Convert.FromBase64String(imageDataProp.GetString()!);
                }

                else if (productElement.TryGetProperty("ImageData", out var imageDataProp2) &&
                    imageDataProp2.ValueKind == JsonValueKind.String)
                {
                    imageData= Convert.FromBase64String(imageDataProp2.GetString()!);
                }

                if(productElement.TryGetProperty("imageFileName", out var fileNameProp))
                {
                    imageFileName=fileNameProp.GetString();
                }

                else if(productElement.TryGetProperty("ImageFileName", out var fileNameProp2))
                {
                    imageFileName=fileNameProp2.GetString();
                }

                // If we have image data, use multipart form data
                if (imageData != null && imageFileName != null)
                {
                    using var formContent = new MultipartFormDataContent
                    {
                        //Add all product fields
                        { new StringContent(productData.inventoryCode.ToString()), "InventoryCode" },
                        { new StringContent(productData.model ?? ""), "Model" },
                        { new StringContent(productData.vendor ?? ""), "Vendor" },
                        { new StringContent(productData.worker ?? ""), "Worker" },
                        { new StringContent(productData.description ?? ""), "Description" },
                        { new StringContent(productData.isWorking.ToString()), "IsWorking" },
                        { new StringContent(productData.isActive.ToString()), "IsActive" },
                        { new StringContent(productData.isNewItem.ToString()), "IsNewItem" },
                        { new StringContent(productData.categoryId.ToString()), "CategoryId" },
                        { new StringContent(productData.departmentId.ToString()), "DepartmentId" }
                    };

                    // Add image file
                    var imageContent = new ByteArrayContent(imageData);
                    imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                    formContent.Add(imageContent, "ImageFile", imageFileName);

                    var response = await _httpClient.PostAsync(
                        $"{_configuration["Services:ProductService"]}/api/products/approved/multipart",
                        formContent,
                        cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError($"Failed to create product with image: {response.StatusCode} - {responseContent}");
                    }

                    return response.IsSuccessStatusCode;
                }
                else
                {
                    // No image, use JSON
                    var json = JsonSerializer.Serialize(productData);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(
                        $"{_configuration["Services:ProductService"]}/api/products/approved",
                        content,
                        cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError($"Failed to create product: {response.StatusCode} - {responseContent}");
                    }

                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing create product");
                return false;
            }
        }

        private async Task<bool> ExecuteUpdateProduct(string actionData, CancellationToken cancellationToken)
        {
            try
            {
                var jsonDoc = JsonDocument.Parse(actionData);
                var root = jsonDoc.RootElement;

                //Extract ProductId
                var productId = root.GetProperty("ProductId").GetInt32();

                // Extract the UpdateData which contains the actual update fields
                JsonElement updateDataElement;
                if(root.TryGetProperty("UpdateData", out var updateDataProp))
                {
                    updateDataElement = updateDataProp;
                }
                else if(root.TryGetProperty("updateData", out var updateDataProp2))
                {
                    updateDataElement = updateDataProp2;
                }
                else
                {
                    _logger.LogError("UpdateData not found in action data");
                    return false;
                }

                // Create the DTO structure that the API expects
                var updateDto = new
                {
                    model = GetStringProperty(updateDataElement, "model", "Model"),
                    vendor = GetStringProperty(updateDataElement, "vendor", "Vendor"),
                    worker = GetStringProperty(updateDataElement, "worker", "Worker"),
                    description = GetStringProperty(updateDataElement, "description", "Description"),
                    categoryId = GetIntProperty(updateDataElement, "categoryId", "CategoryId"),
                    departmentId = GetIntProperty(updateDataElement, "departmentId", "DepartmentId")
                };

                var json = JsonSerializer.Serialize(updateDto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(
                    $"{_configuration["Services:ProductService"]}/api/products/{productId}/approved",
                    content,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to update product {productId}: {response.StatusCode} - {responseContent}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing update product");
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