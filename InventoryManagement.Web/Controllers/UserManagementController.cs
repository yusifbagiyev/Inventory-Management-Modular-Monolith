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

        public UserManagementController(
            IUserManagementService userManagementService,
            ILogger<UserManagementController> logger)
        {
            _userManagementService = userManagementService;
            _logger = logger;
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
                    var result = await _userManagementService.UpdateUserAsync(model);
                    if (result)
                    {
                        TempData["SuccessMessage"] = "User updated successfully!";
                        return RedirectToAction(nameof(Index));
                    }
                    else
                    {
                        ModelState.AddModelError("", "Failed to update user. Please try again.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating user");
                    ModelState.AddModelError("", "An error occurred while updating the user.");
                }
            }

            // Reload available roles if validation fails
            try
            {
                var roles = await _userManagementService.GetAllRolesAsync();
                model.AvailableRoles = roles.Select(r => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = r,
                    Text = r,
                    Selected = model.CurrentRoles.Contains(r)
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reloading roles");
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
        public async Task<IActionResult> Permissions()
        {
            try
            {
                var permissions = await _userManagementService.GetAllPermissionsAsync();
                return View(permissions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving permissions");
                TempData["ErrorMessage"] = "Error retrieving permissions. Please try again.";
                return View(new List<PermissionViewModel>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> Roles()
        {
            try
            {
                var roles = await _userManagementService.GetAllRolesAsync();
                return View(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving roles");
                TempData["ErrorMessage"] = "Error retrieving roles. Please try again.";
                return View(new List<string>());
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
    }
}