using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedServices.Identity;
using IdentityAuth = IdentityService.Application.Services.IAuthService;

namespace InventoryManagement.Web.Controllers
{
    [Authorize(Roles = AllRoles.Admin)]
    public class UserManagementController : BaseController
    {
        private readonly IUserManagementService _userManagementService;
        private readonly IdentityAuth _identity;

        public UserManagementController(
            IUserManagementService userManagementService,
            IdentityAuth identity,
            ILogger<UserManagementController> logger)
            : base(logger)
        {
            _userManagementService = userManagementService;
            _identity = identity;
        }

        [HttpGet]
        public async Task<IActionResult> Index() => View(await _userManagementService.GetAllUsersAsync());


        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new CreateUserViewModel();
            await LoadRoles(model);
            return View(model);
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

            var success = await _userManagementService.CreateUserAsync(model);
            if (IsAjaxRequest())
                return AjaxResponse(success, success ? "User created successfully" : "Failed to create user");

            if (success)
            {
                TempData["Success"] = Tr("User created successfully");
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError("", Tr("Failed to create user"));
            await LoadRoles(model);
            return View(model);
        }


        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userManagementService.GetUserByIdAsync(id);
            return user == null ? RedirectToNotFound() : View(user);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadRoles(model);
                return HandleValidationErrors(model);
            }

            var success = await _userManagementService.UpdateUserAsync(model);
            if (IsAjaxRequest())
                return AjaxResponse(success, success ? "User updated successfully" : "Failed to update user");

            if (success)
            {
                TempData["Success"] = Tr("User updated successfully");
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError("", Tr("Failed to update user"));
            await LoadRoles(model);
            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _userManagementService.DeleteUserAsync(id);
            if (IsAjaxRequest())
                return AjaxResponse(success, success ? "User deleted successfully" : "Failed to delete user");

            TempData[success ? "Success" : "Error"] = Tr(success ? "User deleted successfully" : "Failed to delete user");
            return RedirectToAction(nameof(Index));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var success = await _userManagementService.ToggleUserStatusAsync(id);
            return IsAjaxRequest()
                ? AjaxResponse(success, success ? "User status updated successfully" : "Failed to update user status")
                : RedirectToAction(nameof(Index));
        }


        [HttpGet]
        public async Task<IActionResult> ResetPassword(int id)
        {
            var user = await _userManagementService.GetUserByIdAsync(id);
            return user == null
                ? RedirectToNotFound()
                : View(new ResetPasswordViewModel { UserId = user.Id, Username = user.Username });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return HandleValidationErrors(model);

            var success = await _userManagementService.ResetPasswordAsync(model.UserId, model.NewPassword);
            if (IsAjaxRequest())
                return AjaxResponse(success, success ? "Password reset successfully" : "Failed to reset password");

            if (success)
            {
                TempData["Success"] = Tr("Password reset successfully");
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError("", Tr("Failed to reset password"));
            return View(model);
        }


        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManagementService.GetUserByIdAsync(id);
            return user == null ? RedirectToNotFound() : View(user);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> QuickToggleStatus(int id)
            => Json(new { success = await _userManagementService.ToggleUserStatusAsync(id) });


        /// <summary>
        /// Every permission with whether the user holds it directly (toggleable here) or only
        /// through a role (shown, but revoking a direct grant does not remove it).
        /// </summary>
        [HttpGet]
        public async Task<JsonResult> GetUserPermissions(int id)
        {
            var user = await _identity.GetUserAsync(id);
            if (user == null)
                return Json(new { error = Tr("User not found") });

            var direct = (await _identity.GetUserDirectPermissionsAsync(id)).Select(p => p.Name).ToHashSet();
            var permissions = await _identity.GetAllPermissionsAsync();

            return Json(permissions.Select(p => new
            {
                p.Id,
                p.Name,
                p.Description,
                p.Category,
                IsAssigned = direct.Contains(p.Name),
                IsFromRole = !direct.Contains(p.Name) && user.Permissions.Contains(p.Name)
            }));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> TogglePermission(int id, [FromBody] TogglePermissionViewModel model)
        {
            var success = model.IsGranting
                ? await _identity.GrantPermissionToUserAsync(id, model.PermissionName, GetCurrentUserName())
                : await _identity.RevokePermissionFromUserAsync(id, model.PermissionName);

            return Json(success
                ? new { success = true, message = (string?)null }
                : new { success = false, message = (string?)Tr("Permission change failed") });
        }


        private async Task LoadRoles(CreateUserViewModel model)
            => model.Roles = (await _userManagementService.GetAllRolesAsync())
                .Select(r => new SelectListItem { Value = r, Text = r })
                .ToList();

        private async Task LoadRoles(EditUserViewModel model)
            => model.AvailableRoles = (await _userManagementService.GetAllRolesAsync())
                .Select(r => new SelectListItem { Value = r, Text = r, Selected = model.SelectedRoles?.Contains(r) == true })
                .ToList();
    }
}
