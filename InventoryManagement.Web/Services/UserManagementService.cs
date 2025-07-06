using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

namespace InventoryManagement.Web.Services
{
    public class UserManagementService : IUserManagementService
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        public UserManagementService(
            HttpClient httpClient,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
            _httpClient.BaseAddress = new Uri(_configuration["ApiGateway:BaseUrl"] ?? "http://localhost:5000");
            AddAuthorizationHeader();
        }

        private void AddAuthorizationHeader()
        {
            var token = _httpContextAccessor.HttpContext?.Session.GetString("JwtToken");
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task<List<UserListViewModel>> GetAllUsersAsync()
        {
            var response = await _httpClient.GetAsync("api/auth/users");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var users = JsonConvert.DeserializeObject<List<UserDto>>(content);

                return users?.Select(u => new UserListViewModel
                {
                    Id = u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    FullName = $"{u.FirstName} {u.LastName}",
                    IsActive = true, // Add this field to UserDto
                    Roles = u.Roles,
                    CreatedAt = DateTime.Now, // Add these fields to UserDto
                    LastLoginAt = null
                }).ToList() ?? new List<UserListViewModel>();
            }
            return new List<UserListViewModel>();
        }

        public async Task<EditUserViewModel> GetUserByIdAsync(int id)
        {
            var response = await _httpClient.GetAsync($"api/auth/users/{id}");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var user = JsonConvert.DeserializeObject<UserDto>(content);

                if (user != null)
                {
                    var roles = await GetAllRolesAsync();
                    return new EditUserViewModel
                    {
                        Id = user.Id,
                        Username = user.Username,
                        Email = user.Email,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        IsActive = true,
                        CurrentRoles = user.Roles,
                        AvailableRoles = roles.Select(r => new SelectListItem
                        {
                            Value = r,
                            Text = r,
                            Selected = user.Roles.Contains(r)
                        }).ToList()
                    };
                }
            }
            return new EditUserViewModel();
        }

        public async Task<bool> CreateUserAsync(CreateUserViewModel model)
        {
            var registerDto = new
            {
                Username = model.Username,
                Email = model.Email,
                Password = model.Password,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Role = model.SelectedRole
            };

            var json = JsonConvert.SerializeObject(registerDto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("api/auth/register-by-admin", content);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> UpdateUserAsync(EditUserViewModel model)
        {
            var updateDto = new
            {
                Id = model.Id,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                IsActive = model.IsActive,
                Roles = model.SelectedRoles
            };

            var json = JsonConvert.SerializeObject(updateDto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync($"api/auth/users/{model.Id}", content);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteUserAsync(int id)
        {
            var response = await _httpClient.DeleteAsync($"api/auth/users/{id}");
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> ToggleUserStatusAsync(int id)
        {
            var response = await _httpClient.PostAsync($"api/auth/users/{id}/toggle-status", null);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> ResetPasswordAsync(int userId, string newPassword)
        {
            var resetDto = new { UserId = userId, NewPassword = newPassword };
            var json = JsonConvert.SerializeObject(resetDto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"api/auth/users/{userId}/reset-password", content);
            return response.IsSuccessStatusCode;
        }

        public async Task<List<string>> GetAllRolesAsync()
        {
            var response = await _httpClient.GetAsync("api/auth/roles");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<string>>(content) ?? new List<string>();
            }
            return new List<string> { "Admin", "Manager", "User" };
        }

        public async Task<List<PermissionViewModel>> GetAllPermissionsAsync()
        {
            var response = await _httpClient.GetAsync("api/auth/permissions");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var permissions = JsonConvert.DeserializeObject<List<dynamic>>(content);

                return permissions?.Select(p => new PermissionViewModel
                {
                    Id = p.id,
                    Name = p.name,
                    Description = p.description
                }).ToList() ?? new List<PermissionViewModel>();
            }
            return new List<PermissionViewModel>();
        }

        public async Task<ManagePermissionsViewModel> GetRolePermissionsAsync(int roleId)
        {
            // Implementation for getting role permissions
            return new ManagePermissionsViewModel();
        }

        public async Task<bool> UpdateRolePermissionsAsync(int roleId, List<int> permissionIds)
        {
            var json = JsonConvert.SerializeObject(permissionIds);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync($"api/auth/roles/{roleId}/permissions", content);
            return response.IsSuccessStatusCode;
        }
    }
}