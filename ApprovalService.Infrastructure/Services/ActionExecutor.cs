using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ApprovalService.Application.DTOs;
using ApprovalService.Application.Interfaces;
using ApprovalService.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

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
            //Use system token for executing approved actions
            var systemToken = _configuration["SystemToken"];
            if (!string.IsNullOrEmpty(systemToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", systemToken);
            }
        }
        private async Task<bool> ExecuteCreateProduct(string actionData,CancellationToken cancellationToken  = default)
        {
            var data = JsonSerializer.Deserialize<CreateProductActionData>(actionData);

            var response=await _httpClient.PostAsync(
                $"{_configuration["Services:ProductService"]}/api/products/approved",
                new StringContent(actionData,Encoding.UTF8,"application/json"),
                cancellationToken);
            return response.IsSuccessStatusCode;
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