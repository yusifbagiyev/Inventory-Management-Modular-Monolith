using InventoryManagement.Web.Filters;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedServices.Identity;
using IdentityAuth = IdentityService.Application.Services.IAuthService;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
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
        [PermissionAuthorize(AllPermissions.UserView)]
        public async Task<IActionResult> Index() => View(await _userManagementService.GetAllUsersAsync());


        [HttpGet]
        [PermissionAuthorize(AllPermissions.UserManage)]
        public async Task<IActionResult> Create()
        {
            var model = new CreateUserViewModel();
            await LoadRoles(model);
            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AllPermissions.UserManage)]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadRoles(model);
                return HandleValidationErrors(model);
            }

            // Only Admins choose the role; everyone else creates plain users.
            if (!User.IsInRole(AllRoles.Admin))
                model.SelectedRole = AllRoles.User;

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
        [PermissionAuthorize(AllPermissions.UserManage)]
        public async Task<IActionResult> Edit(int id)
        {
            if (await IsProtectedAsync(id))
                return Forbidden();

            var user = await _userManagementService.GetUserByIdAsync(id);
            if (user == null)
                return RedirectToNotFound();

            // The permission editor: Admins only, and not for Admin accounts (they hold everything).
            if (User.IsInRole(AllRoles.Admin) && !user.CurrentRoles.Contains(AllRoles.Admin))
            {
                var own = (await _identity.GetUserDirectPermissionsAsync(id)).Select(p => p.Name);
                var fromRole = new List<string>();
                foreach (var role in user.CurrentRoles)
                    fromRole.AddRange(await _identity.GetRolePermissionsAsync(role));
                ViewBag.PermissionEditor = await EditorAsync(own, fromRole, Url.Action(nameof(TogglePermission), new { id })!);
            }
            return View(user);
        }

        /// <summary>
        /// What everyone with <paramref name="role"/> holds (users add their own on top). Changing a
        /// user's role changes these for them. The Admin role holds everything and is not edited.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = AllRoles.Admin)]
        public async Task<IActionResult> RolePermissions(string role = AllRoles.User)
        {
            if (role == AllRoles.Admin || !(await _userManagementService.GetAllRolesAsync()).Contains(role))
                return RedirectToAction(nameof(Index));

            ViewBag.Role = role;
            ViewBag.UserCount = (await _userManagementService.GetAllUsersAsync()).Count(u => u.Roles.Contains(role));
            return View(await EditorAsync(await _identity.GetRolePermissionsAsync(role), [],
                Url.Action(nameof(ToggleRolePermission), new { role })!));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AllRoles.Admin)]
        public async Task<JsonResult> ToggleRolePermission(string role, [FromBody] TogglePermissionViewModel model)
        {
            var success = role != AllRoles.Admin
                && await _identity.SetRolePermissionAsync(role, model.PermissionName, model.IsGranting);
            return Json(success
                ? new { success = true, message = (string?)null }
                : new { success = false, message = (string?)Tr("Permission change failed") });
        }

        private async Task<PermissionEditorModel> EditorAsync(IEnumerable<string> own, IEnumerable<string> fromRole, string saveUrl)
        {
            var others = (await _identity.GetAllPermissionsAsync())
                .Where(p => !PermissionCatalog.Known.Contains(p.Name))
                .Select(p => (p.Name, (string?)p.Description))
                .ToList();
            return new PermissionEditorModel(
                own.ToHashSet(StringComparer.OrdinalIgnoreCase),
                fromRole.ToHashSet(StringComparer.OrdinalIgnoreCase),
                saveUrl, others);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AllPermissions.UserManage)]
        public async Task<IActionResult> Edit(EditUserViewModel model)
        {
            if (await IsProtectedAsync(model.Id))
                return Forbidden();

            if (!ModelState.IsValid)
            {
                await LoadRoles(model);
                return HandleValidationErrors(model);
            }

            // Roles are changed by Admins only (the form shows them to Admins only).
            if (!User.IsInRole(AllRoles.Admin))
                model.SelectedRoles = null;

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
        [PermissionAuthorize(AllPermissions.UserManage)]
        public async Task<IActionResult> Delete(int id)
        {
            if (await IsProtectedAsync(id))
                return Forbidden();

            var success = await _userManagementService.DeleteUserAsync(id);
            if (IsAjaxRequest())
                return AjaxResponse(success, success ? "User deleted successfully" : "Failed to delete user");

            TempData[success ? "Success" : "Error"] = Tr(success ? "User deleted successfully" : "Failed to delete user");
            return RedirectToAction(nameof(Index));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AllPermissions.UserManage)]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            if (await IsProtectedAsync(id))
                return Forbidden();

            var success = await _userManagementService.ToggleUserStatusAsync(id);
            return IsAjaxRequest()
                ? AjaxResponse(success, success ? "User status updated successfully" : "Failed to update user status")
                : RedirectToAction(nameof(Index));
        }


        [HttpGet]
        [PermissionAuthorize(AllPermissions.UserManage)]
        public async Task<IActionResult> ResetPassword(int id)
        {
            if (await IsProtectedAsync(id))
                return Forbidden();

            var user = await _userManagementService.GetUserByIdAsync(id);
            return user == null
                ? RedirectToNotFound()
                : View(new ResetPasswordViewModel { UserId = user.Id, Username = user.Username });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AllPermissions.UserManage)]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (await IsProtectedAsync(model.UserId))
                return Forbidden();

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
        [PermissionAuthorize(AllPermissions.UserView)]
        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManagementService.GetUserByIdAsync(id);
            return user == null ? RedirectToNotFound() : View(user);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AllPermissions.UserManage)]
        public async Task<JsonResult> QuickToggleStatus(int id)
        {
            if (await IsProtectedAsync(id))
                return Json(new { success = false });
            return Json(new { success = await _userManagementService.ToggleUserStatusAsync(id) });
        }


        /// <summary>Granting permissions is Admin-only: a holder could otherwise grant themselves anything.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AllRoles.Admin)]
        public async Task<JsonResult> TogglePermission(int id, [FromBody] TogglePermissionViewModel model)
        {
            var success = model.IsGranting
                ? await _identity.GrantPermissionToUserAsync(id, model.PermissionName, GetCurrentUserName())
                : await _identity.RevokePermissionFromUserAsync(id, model.PermissionName);

            return Json(success
                ? new { success = true, message = (string?)null }
                : new { success = false, message = (string?)Tr("Permission change failed") });
        }


        /// <summary>
        /// Admin accounts can be changed by Admins only: a user.manage holder must not be able to
        /// reset an Admin's password or deactivate them.
        /// </summary>
        private async Task<bool> IsProtectedAsync(int userId)
        {
            if (User.IsInRole(AllRoles.Admin))
                return false;
            var target = await _identity.GetUserAsync(userId);
            return target?.Roles.Contains(AllRoles.Admin) == true;
        }

        private IActionResult Forbidden() => IsAjaxRequest()
            ? StatusCode(StatusCodes.Status403Forbidden, new { isSuccess = false, success = false, message = Tr("Only an administrator can change an administrator account.") })
            : RedirectToAction("AccessDenied", "Account");

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
