using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InventoryManagement.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly IUserManagementService _userService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IUserManagementService userService, ILogger<UsersController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userService.GetAllUsersAsync();
            return View(users);
        }

        public async Task<IActionResult> Create()
        {
            var model = new CreateUserViewModel
            {
                Roles = (await _userService.GetAllRolesAsync())
                    .Select(r => new SelectListItem { Value = r, Text = r })
                    .ToList()
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var result = await _userService.CreateUserAsync(model);
                    if (result)
                    {
                        TempData["Success"] = "User created successfully!";
                        return RedirectToAction(nameof(Index));
                    }
                    ModelState.AddModelError("", "Failed to create user");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating user");
                    ModelState.AddModelError("", "An error occurred");
                }
            }

            model.Roles = (await _userService.GetAllRolesAsync())
                .Select(r => new SelectListItem { Value = r, Text = r })
                .ToList();
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
                return NotFound();

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var result = await _userService.UpdateUserAsync(model);
                    if (result)
                    {
                        TempData["Success"] = "User updated successfully!";
                        return RedirectToAction(nameof(Index));
                    }
                    ModelState.AddModelError("", "Failed to update user");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating user");
                    ModelState.AddModelError("", "An error occurred");
                }
            }

            var user = await _userService.GetUserByIdAsync(model.Id);
            model.AvailableRoles = user.AvailableRoles;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var result = await _userService.DeleteUserAsync(id);
                if (result)
                {
                    TempData["Success"] = "User deleted successfully!";
                }
                else
                {
                    TempData["Error"] = "Failed to delete user";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user");
                TempData["Error"] = "An error occurred";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var result = await _userService.ToggleUserStatusAsync(id);
            return Json(new { success = result });
        }

        public async Task<IActionResult> ResetPassword(int id)
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
                return NotFound();

            var model = new ResetPasswordViewModel
            {
                UserId = id,
                Username = user.Username
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _userService.ResetPasswordAsync(model.UserId, model.NewPassword);
                if (result)
                {
                    TempData["Success"] = "Password reset successfully!";
                    return RedirectToAction(nameof(Index));
                }
                ModelState.AddModelError("", "Failed to reset password");
            }

            return View(model);
        }

        public async Task<IActionResult> Permissions()
        {
            var roles = await _userService.GetAllRolesAsync();
            ViewBag.Roles = roles.Select(r => new SelectListItem
            {
                Value = r,
                Text = r
            }).ToList();

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetRolePermissions(string roleName)
        {
            // Get permissions for the selected role
            var permissions = await _userService.GetAllPermissionsAsync();

            // Group permissions by category
            var groupedPermissions = permissions.GroupBy(p => GetPermissionCategory(p.Name))
                .Select(g => new PermissionGroupViewModel
                {
                    Category = g.Key,
                    Permissions = g.ToList()
                }).ToList();

            return PartialView("_PermissionsPartial", groupedPermissions);
        }

        private string GetPermissionCategory(string permissionName)
        {
            if (permissionName.StartsWith("product.")) return "Product";
            if (permissionName.StartsWith("route.")) return "Route";
            if (permissionName.StartsWith("user.")) return "User";
            if (permissionName.StartsWith("role.")) return "Role";
            if (permissionName.StartsWith("approval.")) return "Approval";
            return "Other";
        }
    }
}