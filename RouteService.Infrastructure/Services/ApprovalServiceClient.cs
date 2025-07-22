using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using RouteService.Application.DTOs;
using RouteService.Application.Interfaces;
using SharedServices.DTOs;

namespace RouteService.Infrastructure.Services
{
    public class ApprovalServiceClient : IApprovalService
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ApprovalServiceClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("http://localhost:5000"); // Via API Gateway
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ApprovalRequestDto> CreateApprovalRequestAsync(CreateApprovalRequestDto dto, int userId, string userName)
        {
            var authHeader = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", authHeader.Replace("Bearer ", ""));
            }

            var content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/api/approvalrequests", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApprovalRequestDto>(responseContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result ?? throw new InvalidOperationException("Failed to create approval request");
        }
    }
}