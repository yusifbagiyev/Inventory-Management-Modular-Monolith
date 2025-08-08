using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ApprovalService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SharedServices.DTOs;
using SharedServices.Enum;

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
                    RequestType.UpdateRoute => await ExecuteUpdateRoute(actionData, cancellationToken),
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
                Subject = new ClaimsIdentity([
                    new Claim(ClaimTypes.Role, "Admin"),
                    new Claim(ClaimTypes.NameIdentifier, "0"), // System user
                    new Claim(ClaimTypes.Name, "System")
                ]),
                Expires = DateTime.Now.AddMinutes(5),
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
                        { new StringContent(GetIntProperty(productElement, "inventoryCode", "InventoryCode").ToString()), "InventoryCode" },
                        { new StringContent(GetStringProperty(productElement, "model", "Model")), "Model" },
                        { new StringContent(GetStringProperty(productElement, "vendor", "Vendor")), "Vendor" },
                        { new StringContent(GetStringProperty(productElement, "worker", "Worker")), "Worker" },
                        { new StringContent(GetStringProperty(productElement, "description", "Description")), "Description" },
                        { new StringContent(GetBoolProperty(productElement, "isWorking", "IsWorking", true).ToString()), "IsWorking" },
                        { new StringContent(GetBoolProperty(productElement, "isActive", "IsActive", true).ToString()), "IsActive" },
                        { new StringContent(GetBoolProperty(productElement, "isNewItem", "IsNewItem", true).ToString()), "IsNewItem" },
                        { new StringContent(GetIntProperty(productElement, "categoryId", "CategoryId").ToString()), "CategoryId" },
                        { new StringContent(GetIntProperty(productElement, "departmentId", "DepartmentId").ToString()), "DepartmentId" }
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
                        departmentId = GetIntProperty(productElement, "departmentId", "DepartmentId")
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
                // Check for image data
                byte[]? imageData = null;
                string? imageFileName = null;

                if (updateDataElement.TryGetProperty("imageData", out var imageDataProp) &&
                    imageDataProp.ValueKind == JsonValueKind.String)
                {
                    imageData = Convert.FromBase64String(imageDataProp.GetString()!);
                }
                else if (updateDataElement.TryGetProperty("ImageData", out var imageDataProp2) &&
                    imageDataProp2.ValueKind == JsonValueKind.String)
                {
                    imageData = Convert.FromBase64String(imageDataProp2.GetString()!);
                }

                if (updateDataElement.TryGetProperty("imageFileName", out var fileNameProp))
                {
                    imageFileName = fileNameProp.GetString();
                }
                else if (updateDataElement.TryGetProperty("ImageFileName", out var fileNameProp2))
                {
                    imageFileName = fileNameProp2.GetString();
                }

                // Use multipart form data if we have an image, otherwise use JSON
                if (imageData != null && imageFileName != null)
                {
                    using var formContent = new MultipartFormDataContent
                    {
                        // Add all update fields
                        { new StringContent(GetStringProperty(updateDataElement, "model", "Model")), "Model" },
                        { new StringContent(GetStringProperty(updateDataElement, "vendor", "Vendor")), "Vendor" },
                        { new StringContent(GetStringProperty(updateDataElement, "worker", "Worker")), "Worker" },
                        { new StringContent(GetStringProperty(updateDataElement, "description", "Description")), "Description" },
                        { new StringContent(GetIntProperty(updateDataElement, "categoryId", "CategoryId").ToString()), "CategoryId" },
                        { new StringContent(GetIntProperty(updateDataElement, "departmentId", "DepartmentId").ToString()), "DepartmentId" }
                    };

                    // Add image file
                    var imageContent = new ByteArrayContent(imageData);
                    imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                    formContent.Add(imageContent, "ImageFile", imageFileName);

                    var response = await _httpClient.PutAsync(
                        $"{_configuration["Services:ProductService"]}/api/products/{productId}/approved",
                        formContent,
                        cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError($"Failed to update product {productId}: {response.StatusCode} - {responseContent}");
                    }

                    return response.IsSuccessStatusCode;
                }
                else
                {
                    // No image, use JSON
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing delete product");
                return false;
            }
        }

        private async Task<bool> ExecuteTransferProduct(string actionData, CancellationToken cancellationToken)
        {
            try
            {
                var jsonDoc = JsonDocument.Parse(actionData);
                var root = jsonDoc.RootElement;

                // Create form content for multipart request
                using var formContent = new MultipartFormDataContent();

                // Get the productId to handle both camelCase and pascalCase
                var productId = root.TryGetProperty("productId", out var pid)
                    ? pid.GetInt32()
                    : root.GetProperty("ProductId").GetInt32();

                formContent.Add(new StringContent(productId.ToString()), "ProductId");

                // Get department ID
                var toDepartmentId = root.TryGetProperty("toDepartmentId", out var deptId)
                    ? deptId.GetInt32()
                    : root.GetProperty("ToDepartmentId").GetInt32();

                formContent.Add(new StringContent(toDepartmentId.ToString()), "ToDepartmentId");

                // Add optional fields
                if (root.TryGetProperty("toWorker", out var worker) && worker.ValueKind != JsonValueKind.Null)
                {
                    formContent.Add(new StringContent(worker.GetString() ?? ""), "ToWorker");
                }
                else if (root.TryGetProperty("ToWorker", out var workerPascal) && workerPascal.ValueKind != JsonValueKind.Null)
                {
                    formContent.Add(new StringContent(workerPascal.GetString() ?? ""), "ToWorker");
                }

                if (root.TryGetProperty("notes", out var notes) && notes.ValueKind != JsonValueKind.Null)
                {
                    formContent.Add(new StringContent(notes.GetString() ?? ""), "Notes");
                }
                else if (root.TryGetProperty("Notes", out var notesPascal) && notesPascal.ValueKind != JsonValueKind.Null)
                {
                    formContent.Add(new StringContent(notesPascal.GetString() ?? ""), "Notes");
                }

                // Check for image data in multiple possible formats
                byte[]? imageData = null;
                string? imageFileName = null;

                if (root.TryGetProperty("imageData", out var imageDataProp) &&
                    imageDataProp.ValueKind == JsonValueKind.String)
                {
                    imageData = Convert.FromBase64String(imageDataProp.GetString()!);
                }
                else if (root.TryGetProperty("ImageData", out var imageDataProp2) &&
                    imageDataProp2.ValueKind == JsonValueKind.String)
                {
                    imageData = Convert.FromBase64String(imageDataProp2.GetString()!);
                }
                else if (root.TryGetProperty("image", out var imageProp) && imageProp.ValueKind == JsonValueKind.String)
                {
                    imageData = Convert.FromBase64String(imageProp.GetString()!);
                }

                if (root.TryGetProperty("imageFileName", out var fileNameProp))
                {
                    imageFileName = fileNameProp.GetString();
                }
                else if (root.TryGetProperty("ImageFileName", out var fileNameProp2))
                {
                    imageFileName = fileNameProp2.GetString();
                }

                if (imageData != null && !string.IsNullOrEmpty(imageFileName))
                {
                    var imageContent = new ByteArrayContent(imageData);
                    imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                    formContent.Add(imageContent, "ImageFile", imageFileName);
                }

                _logger.LogInformation($"Executing transfer for product {productId} to department {toDepartmentId}");

                var response = await _httpClient.PostAsync(
                    $"{_configuration["Services:RouteService"]}/api/inventoryroutes/transfer/approved",
                    formContent,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Transfer failed: {response.StatusCode} - {responseContent}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing transfer product");
                return false;
            }
        }

        private async Task<bool> ExecuteUpdateRoute(string actionData, CancellationToken cancellationToken)
        {
            try
            {
                var jsonDoc = JsonDocument.Parse(actionData);
                var root = jsonDoc.RootElement;

                var routeId=root.GetProperty("RouteId").GetInt32();
                var updateData=root.GetProperty("UpdateData");

                var json= JsonSerializer.Serialize(updateData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PutAsync(
                    $"{_configuration["Services:RouteService"]}/api/inventoryroutes/{routeId}/approved",
                    content,
                    cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing update route");
                throw;
            }
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