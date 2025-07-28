using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InventoryManagement.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UserManagementController : BaseController
    {
        private readonly IUserManagementService _userManagementService;
        private readonly IApiService _apiService;

        public UserManagementController(
            IUserManagementService userManagementService,
            ILogger<UserManagementController> logger,
            IApiService apiService) :base(logger)
        {
            _userManagementService = userManagementService;
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
                return HandleException(ex, new List<UserListViewModel>());
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
                    Roles = roles.Select(r => new SelectListItem
                    {
                        Value = r,
                        Text = r
                    }).ToList()
                };
                return View(model);
            }
            catch
            {
                TempData["ErrorMessage"] = "Error loading form. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadRoles(model);
                return HandleValidationErrors(model);
            }

            try
            {
                var success = await _userManagementService.CreateUserAsync(model);

                if (IsAjaxRequest())
                {
                    return AjaxResponse(success,
                        success ? "User created successfully" : "Failed to create user");
                }

                if (success)
                {
                    TempData["Success"] = "User created successfully";
                    return RedirectToAction("Index");
                }

                ModelState.AddModelError("", "Failed to create user");
                await LoadRoles(model);
                return View(model);
            }
            catch (Exception ex)
            {
                await LoadRoles(model);
                return HandleException(ex, model);
            }
        }


        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var user = await _userManagementService.GetUserByIdAsync(id);
                if (user == null)
                    return NotFound();

                return View(user);
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return HandleValidationErrors(model);
            }

            try
            {
                var success = await _userManagementService.UpdateUserAsync(model);

                if (IsAjaxRequest())
                {
                    return AjaxResponse(success,
                        success ? "User updated successfully" : "Failed to update user");
                }

                if (success)
                {
                    TempData["Success"] = "User updated successfully";
                    return RedirectToAction("Index");
                }

                ModelState.AddModelError("", "Failed to update user");
                return View(model);
            }
            catch (Exception ex)
            {
                return HandleException(ex, model);
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var success = await _userManagementService.DeleteUserAsync(id);

                if (IsAjaxRequest())
                {
                    return AjaxResponse(success,
                        success ? "User deleted successfully" : "Failed to delete user");
                }

                if (success)
                {
                    TempData["Success"] = "User deleted successfully";
                }
                else
                {
                    TempData["Error"] = "Failed to delete user";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            try
            {
                var success = await _userManagementService.ToggleUserStatusAsync(id);
                if (IsAjaxRequest())
                {
                    return AjaxResponse(success,
                        success ? "User status updated successfully" : "Failed to update user status");
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }


        [HttpGet]
        public async Task<IActionResult> ResetPassword(int id)
        {
            try
            {
                var user = await _userManagementService.GetUserByIdAsync(id);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return NotFound();
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
                return HandleException(ex);
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return HandleValidationErrors(model);
            }

            try
            {
                var success = await _userManagementService.ResetPasswordAsync(model.UserId, model.NewPassword);

                if (IsAjaxRequest())
                {
                    return AjaxResponse(success,
                        success ? "Password reset successfully" : "Failed to reset password");
                }

                if (success)
                {
                    TempData["Success"] = "Password reset successfully";
                    return RedirectToAction("Edit", new { id = model.UserId });
                }

                ModelState.AddModelError("", "Failed to reset password");
                return View(model);
            }
            catch (Exception ex)
            {
                return HandleException(ex, model);
            }
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
                return HandleException(ex);
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
            catch
            {
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
            catch
            {
                return Json(new { success = false, message = "Error updating user status." });
            }
        }


        [HttpGet]
        public async Task<JsonResult> GetUserPermissions(int id)
        {
            try
            {
                // Get user details to get their current permissions
                var user = await _apiService.GetAsync<UserDto>($"/api/auth/users/{id}");
                if(user == null)
                {
                    return Json(new { error = "User not found" });
                }

                // Get all available permissions
                var allPermissions = await _apiService.GetAsync<List<PermissionViewModel>>("/api/auth/permissions")
                    ?? [];

                // Create a simple structure showing which permissions are assigned
                var permissionStatus=allPermissions.Select(p=>new
                {
                    p.Id,
                    p.Name,
                    p.Description,
                    p.Category,
                    IsAssigned=user.Permissions.Contains(p.Name),
                    IsFromRole=false
                }).ToList();

                return Json(permissionStatus);
            }
            catch
            {
                return Json(new { error = "Failed to load permissions" });
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
        public async Task<JsonResult> TogglePermission(int id, [FromBody] TogglePermissionViewModel model)
        {
            try
            {
                var url = model.IsGranting
                    ? $"/api/auth/users/{id}/grant-permission"
                    : $"/api/auth/users/{id}/revoke-permission";

                var result = await _apiService.PostAsync<object, bool>(url,
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


        private async Task LoadRoles(CreateUserViewModel model)
        {
            try
            {
                var roles = await _userManagementService.GetAllRolesAsync();
                model.Roles = roles.Select(r => new SelectListItem
                {
                    Value = r,
                    Text = r
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load roles");
                model.Roles = new List<SelectListItem>();
            }
        }
    }
}