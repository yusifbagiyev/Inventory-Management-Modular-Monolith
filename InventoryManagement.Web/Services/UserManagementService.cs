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
        private readonly ILogger<UserManagementService> _logger;

        public UserManagementService(
            HttpClient httpClient,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            ILogger<UserManagementService> logger)
        {
            _httpClient = httpClient;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
            _logger = logger;
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
            try
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
                        IsActive = u.IsActive,
                        Roles = u.Roles,
                        CreatedAt = u.CreatedAt,
                        LastLoginAt = u.LastLoginAt
                    }).ToList() ?? new List<UserListViewModel>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all users");
            }
            return new List<UserListViewModel>();
        }


        public async Task<EditUserViewModel> GetUserByIdAsync(int id)
        {
            try
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
                            IsActive = user.IsActive,
                            CurrentRoles = user.Roles,
                            SelectedRoles = user.Roles,
                            AvailableRoles = roles.Select(r => new SelectListItem
                            {
                                Value = r,
                                Text = r,
                                Selected = user.Roles.Contains(r)
                            }).ToList()
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by id: {UserId}", id);
            }
            return new EditUserViewModel();
        }


        public async Task<bool> CreateUserAsync(CreateUserViewModel model)
        {
            try
            {
                var registerDto = new
                {
                    model.Username,
                    model.Email,
                    model.Password,
                    model.FirstName,
                    model.LastName,
                    model.SelectedRole
                };

                var json = JsonConvert.SerializeObject(registerDto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("api/auth/register-by-admin", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                return false;
            }
        }


        public async Task<bool> UpdateUserAsync(EditUserViewModel model)
        {
            try
            {
                var updateDto = new
                {
                    model.Id,
                    model.Username,
                    model.Email,
                    model.FirstName,
                    model.LastName,
                    model.IsActive
                };

                var json = JsonConvert.SerializeObject(updateDto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync($"api/auth/users/{model.Id}", content);

                // Update roles separately if they've changed
                if (response.IsSuccessStatusCode && model.SelectedRoles != null)
                {
                    // Remove all current roles
                    foreach (var role in model.CurrentRoles)
                    {
                        await _httpClient.PostAsync($"api/auth/users/{model.Id}/remove-role",
                            new StringContent(JsonConvert.SerializeObject(new { roleName = role }),
                            Encoding.UTF8, "application/json"));
                    }

                    // Add selected roles
                    foreach (var role in model.SelectedRoles)
                    {
                        await _httpClient.PostAsync($"api/auth/users/{model.Id}/assign-role",
                            new StringContent(JsonConvert.SerializeObject(new { roleName = role }),
                            Encoding.UTF8, "application/json"));
                    }
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user");
                return false;
            }
        }


        public async Task<bool> DeleteUserAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/auth/users/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user: {UserId}", id);
                return false;
            }
        }


        public async Task<bool> ToggleUserStatusAsync(int id)
        {
            try
            {
                var response = await _httpClient.PostAsync($"api/auth/users/{id}/toggle-status", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling user status: {UserId}", id);
                return false;
            }
        }


        public async Task<bool> ResetPasswordAsync(int userId, string newPassword)
        {
            try
            {
                var resetDto = new ResetPasswordDto { NewPassword = newPassword };
                var json = JsonConvert.SerializeObject(resetDto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"api/auth/users/{userId}/reset-password", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for user: {UserId}", userId);
                return false;
            }
        }

        public async Task<List<string>> GetAllRolesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/auth/roles");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<List<string>>(content) ?? new List<string>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all roles");
            }
            return new List<string> { "Admin", "Manager", "User" };
        }
    }
}