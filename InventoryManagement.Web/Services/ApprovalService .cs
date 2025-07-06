using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Newtonsoft.Json;
using System.Text;

namespace InventoryManagement.Web.Services
{
    public class ApprovalService : IApprovalService
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ApprovalService(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("http://localhost:5004"); // Approval service URL
            _httpContextAccessor = httpContextAccessor;
            AddAuthorizationHeader();
        }

        private void AddAuthorizationHeader()
        {
            var token = _httpContextAccessor.HttpContext?.Session.GetString("JwtToken");
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task<List<ApprovalRequestDto>> GetPendingRequestsAsync()
        {
            var response = await _httpClient.GetAsync("/api/approvalrequests?pageNumber=1&pageSize=100");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var pagedResult = JsonConvert.DeserializeObject<PagedResultDto<ApprovalRequestDto>>(content);
                return pagedResult?.Items.ToList() ?? new List<ApprovalRequestDto>();
            }
            return new List<ApprovalRequestDto>();
        }

        public async Task<ApprovalRequestDto> GetRequestDetailsAsync(int id)
        {
            var response = await _httpClient.GetAsync($"/api/approvalrequests/{id}");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<ApprovalRequestDto>(content);
            }
            return null;
        }

        public async Task ApproveRequestAsync(int id)
        {
            var response = await _httpClient.PostAsync($"/api/approvalrequests/{id}/approve", null);
            response.EnsureSuccessStatusCode();
        }

        public async Task RejectRequestAsync(int id, string reason)
        {
            var content = new StringContent(
                JsonConvert.SerializeObject(new { reason }),
                Encoding.UTF8,
                "application/json");
            var response = await _httpClient.PostAsync($"/api/approvalrequests/{id}/reject", content);
            response.EnsureSuccessStatusCode();
        }

        public async Task<ApprovalRequestDto> CreateApprovalRequestAsync(CreateApprovalRequestDto dto, int userId, string userName)
        {
            var content = new StringContent(
                JsonConvert.SerializeObject(dto),
                Encoding.UTF8,
                "application/json");
            var response = await _httpClient.PostAsync("/api/approvalrequests", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<ApprovalRequestDto>(responseContent);
        }
    }
}