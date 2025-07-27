using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UserManagementController : Controller
    {
        private readonly IUserManagementService _userManagementService;
        private readonly ILogger<UserManagementController> _logger;
        private readonly IApiService _apiService;

        public UserManagementController(
            IUserManagementService userManagementService,
            ILogger<UserManagementController> logger,
            IApiService apiService)
        {
            _userManagementService = userManagementService;
            _logger = logger;
            _apiService = apiService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var users = await _userManagementService.GetAllUsersAsync();
                return View(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users");
                TempData["ErrorMessage"] = "Error retrieving users. Please try again.";
                return View(new List<UserListViewModel>());
            }
        }


        [HttpGet]
        public async Task<IActionResult> Create()
        {
            try
            {
                var roles = await _userManagementService.GetAllRolesAsync();
                var model = new CreateUserViewModel
                {
                    Roles = roles.Select(r => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                    {
                        Value = r,
                        Text = r
                    }).ToList()
                };
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading create user form");
                TempData["ErrorMessage"] = "Error loading form. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var result = await _userManagementService.CreateUserAsync(model);
                    if (result)
                    {
                        TempData["SuccessMessage"] = "User created successfully!";
                        return RedirectToAction(nameof(Index));
                    }
                    else
                    {
                        ModelState.AddModelError("", "Failed to create user. Please try again.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating user");
                    ModelState.AddModelError("", "An error occurred while creating the user.");
                }
            }

            // Reload roles if validation fails
            try
            {
                var roles = await _userManagementService.GetAllRolesAsync();
                model.Roles = roles.Select(r => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = r,
                    Text = r
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reloading roles");
            }

            return View(model);
        }


        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var user = await _userManagementService.GetUserByIdAsync(id);
                if (user == null || user.Id == 0)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction(nameof(Index));
                }
                return View(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user for editing");
                TempData["ErrorMessage"] = "Error retrieving user. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Ensure SelectedRoles is not null (happens when no checkboxes are checked)
                    model.SelectedRoles = model.SelectedRoles ?? new List<string>();

                    // Log the state for debugging
                    _logger.LogInformation("Updating user {UserId} with roles: {Roles}",
                        model.Id, string.Join(", ", model.SelectedRoles));

                    var result = await _userManagementService.UpdateUserAsync(model);
                    if (result)
                    {
                        TempData["SuccessMessage"] = "User updated successfully!";
                        return RedirectToAction(nameof(Index));
                    }
                    else
                    {
                        ModelState.AddModelError("", "Failed to update user. Please check the logs for details.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating user");
                    ModelState.AddModelError("", "An error occurred while updating the user.");
                }
            }

            // If we got here, something failed, redisplay form
            try
            {
                // Reload the current state from the server to ensure data consistency
                var currentUser = await _userManagementService.GetUserByIdAsync(model.Id);

                // Preserve the attempted changes in the model for user feedback
                model.CurrentRoles = currentUser.CurrentRoles;
                model.AvailableRoles = currentUser.AvailableRoles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reloading user data");
            }

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var result = await _userManagementService.DeleteUserAsync(id);
                if (result)
                {
                    TempData["SuccessMessage"] = "User deleted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to delete user. Please try again.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user");
                TempData["ErrorMessage"] = "An error occurred while deleting the user.";
            }

            return RedirectToAction(nameof(Index));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            try
            {
                var result = await _userManagementService.ToggleUserStatusAsync(id);
                if (result)
                {
                    TempData["SuccessMessage"] = "User status updated successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to update user status. Please try again.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling user status");
                TempData["ErrorMessage"] = "An error occurred while updating user status.";
            }

            return RedirectToAction(nameof(Index));
        }


        [HttpGet]
        public async Task<IActionResult> ResetPassword(int id)
        {
            try
            {
                var user = await _userManagementService.GetUserByIdAsync(id);
                if (user == null || user.Id == 0)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction(nameof(Index));
                }

                var model = new ResetPasswordViewModel
                {
                    UserId = user.Id,
                    Username = user.Username
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading reset password form");
                TempData["ErrorMessage"] = "Error loading form. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var result = await _userManagementService.ResetPasswordAsync(model.UserId, model.NewPassword);
                    if (result)
                    {
                        TempData["SuccessMessage"] = "Password reset successfully!";
                        return RedirectToAction(nameof(Index));
                    }
                    else
                    {
                        ModelState.AddModelError("", "Failed to reset password. Please try again.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error resetting password");
                    ModelState.AddModelError("", "An error occurred while resetting the password.");
                }
            }

            return View(model);
        }


        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var user = await _userManagementService.GetUserByIdAsync(id);
                if (user == null || user.Id == 0)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction(nameof(Index));
                }
                return View(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user details");
                TempData["ErrorMessage"] = "Error retrieving user details. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }


        // AJAX endpoints for better UX
        [HttpGet]
        public async Task<JsonResult> GetUser(int id)
        {
            try
            {
                var user = await _userManagementService.GetUserByIdAsync(id);
                return Json(new { success = true, data = user });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user via AJAX");
                return Json(new { success = false, message = "Error retrieving user." });
            }
        }


        [HttpPost]
        public async Task<JsonResult> QuickToggleStatus(int id)
        {
            try
            {
                var result = await _userManagementService.ToggleUserStatusAsync(id);
                return Json(new { success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling user status via AJAX");
                return Json(new { success = false, message = "Error updating user status." });
            }
        }


        [HttpGet]
        public async Task<JsonResult> GetUserPermissionsStatus(int id)
        {
            try
            {
                // Get all available permissions
                var allPermissions = await _apiService.GetAsync<List<PermissionViewModel>>("/api/auth/permissions")
                    ?? [];

                // Get user's direct permissions
                var directPermissions = await _apiService.GetAsync<List<PermissionViewModel>>($"/api/auth/users/{id}/direct-permissions");

                // Get user details to access their roles
                var user = await _apiService.GetAsync<UserDto>($"/api/auth/users/{id}")
                    ?? new UserDto { Id = id, Roles = [] };

                // Get permissions from roles
                var rolePermissions = new List<PermissionViewModel>();
                foreach (var role in user.Roles)
                {
                    // You'll need to add an endpoint to get permissions by role
                    var perms = await _apiService.GetAsync<List<PermissionViewModel>>($"/api/auth/roles/{role}/permissions");
                    if (perms != null)
                        rolePermissions.AddRange(perms);
                }

                // Mark which permissions are assigned and their source
                var permissionStatus = allPermissions.Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Description,
                    p.Category,
                    IsDirect = directPermissions?.Any(dp => dp.Name == p.Name) ?? false,
                    IsFromRole = rolePermissions.Any(rp => rp.Name == p.Name),
                    Roles = user.Roles.Where(r => rolePermissions.Any(rp => rp.Name == p.Name)).ToList()
                }).ToList();

                return Json(permissionStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user permissions status");
                return Json(new { error = "Failed to load permissions" });
            }
        }


        [HttpGet]
        public async Task<JsonResult> GetUserDirectPermissions(int id)
        {
            try
            {
                var permissions = await _apiService.GetAsync<List<PermissionViewModel>>($"/api/auth/users/{id}/direct-permissions");
                return Json(permissions ?? []);
            }
            catch
            {
                return Json(new List<PermissionViewModel>());
            }
        }

        [HttpPost]
        public async Task<JsonResult> GrantPermission(int id, [FromBody] GrantPermissionViewModel model)
        {
            try
            {
                var result = await _apiService.PostAsync<object, bool>($"/api/auth/users/{id}/grant-permission",
                    new { permissionName = model.PermissionName });
                return Json(new { success = result });
            }
            catch
            {
                return Json(new { success = false });
            }
        }

        [HttpPost]
        public async Task<JsonResult> RevokePermission(int id, [FromBody] RevokePermissionViewModel model)
        {
            try
            {
                var result = await _apiService.PostAsync<object, bool>($"/api/auth/users/{id}/revoke-permission",
                    new { permissionName = model.PermissionName });
                return Json(new { success = result });
            }
            catch
            {
                return Json(new { success = false });
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetAllPermissions()
        {
            try
            {
                var permissions = await _apiService.GetAsync<List<PermissionViewModel>>("/api/auth/permissions");
                return Json(permissions ?? []);
            }
            catch
            {
                return Json(new List<PermissionViewModel>());
            }
        }
    }
}