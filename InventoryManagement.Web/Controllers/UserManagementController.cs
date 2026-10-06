using InventoryManagement.Web.Extensions;
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

            // Only Admins choose the role, everyone else creates plain users
            if (!User.IsInRole(AllRoles.Admin))
                model.SelectedRole = AllRoles.User;

            var (success, error) = await _userManagementService.CreateUserAsync(model);
            if (IsAjaxRequest())
                return AjaxResponse(success, success ? "User created successfully" : error ?? "Failed to create user");

            if (success)
            {
                TempData["Success"] = Tr("User created successfully");
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError("", Tr(error ?? "Failed to create user"));
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

            await LoadPermissionEditorAsync(user);
            return View(user);
        }

        /// <summary>Only Admins get the permission editor, and Admin accounts already hold everything.</summary>
        private async Task LoadPermissionEditorAsync(EditUserViewModel user)
        {
            if (!User.IsInRole(AllRoles.Admin) || user.CurrentRoles.Contains(AllRoles.Admin))
                return;

            var own = (await _identity.GetUserDirectPermissionsAsync(user.Id)).Select(p => p.Name);
            var fromRole = new List<string>();
            foreach (var role in user.CurrentRoles)
                fromRole.AddRange(await _identity.GetRolePermissionsAsync(role));
            ViewBag.PermissionEditor = await EditorAsync(own, fromRole, Url.Action(nameof(TogglePermission), new { id = user.Id })!);
        }

        /// <summary>Restores what the Edit form does not post back, so a failed save shows the stored role and the permissions again.</summary>
        private async Task ReloadEditAsync(EditUserViewModel model)
        {
            model.CurrentRoles = (await _identity.GetUserAsync(model.Id))?.Roles ?? [];
            await LoadRoles(model);
            await LoadPermissionEditorAsync(model);
        }

        /// <summary>Edits what every user of a role gets, except the Admin role which holds everything.</summary>
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
                await ReloadEditAsync(model);
                return HandleValidationErrors(model);
            }

            // Only Admins change roles, even if someone posts the field anyway
            if (!User.IsInRole(AllRoles.Admin))
                model.SelectedRoles = null;

            var (success, error) = model.Id == GetCurrentUserId() && !model.IsActive
                ? (false, OwnAccountMessage)
                : await _userManagementService.UpdateUserAsync(model);
            if (IsAjaxRequest())
                return AjaxResponse(success, success ? "User updated successfully" : error ?? "Failed to update user");

            if (success)
            {
                TempData["Success"] = Tr("User updated successfully");
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError("", Tr(error ?? "Failed to update user"));
            await ReloadEditAsync(model);
            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AllPermissions.UserManage)]
        public async Task<IActionResult> Delete(int id)
        {
            if (await IsProtectedAsync(id))
                return Forbidden();

            var (success, error) = id == GetCurrentUserId()
                ? (false, OwnAccountMessage)
                : await _userManagementService.DeleteUserAsync(id);
            var message = success ? "User deleted successfully" : error ?? "Failed to delete user";
            if (IsAjaxRequest())
                return AjaxResponse(success, message);

            TempData[success ? "Success" : "Error"] = Tr(message);
            return RedirectToAction(nameof(Index));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AllPermissions.UserManage)]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            if (await IsProtectedAsync(id))
                return Forbidden();

            var (success, error) = await ToggleStatusAsync(id);
            if (IsAjaxRequest())
                return AjaxResponse(success, success ? "User status updated successfully" : error ?? "Failed to update user status");

            if (!success)
                TempData["Error"] = Tr(error ?? "Failed to update user status");
            return RedirectToAction(nameof(Index));
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

            var (success, error) = await _userManagementService.ResetPasswordAsync(model.UserId, model.NewPassword);
            if (IsAjaxRequest())
                return AjaxResponse(success, success ? "Password reset successfully" : error ?? "Failed to reset password");

            if (success)
            {
                TempData["Success"] = Tr("Password reset successfully");
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError("", Tr(error ?? "Failed to reset password"));
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

            var (success, error) = await ToggleStatusAsync(id);
            return Json(new { success, message = success ? null : Tr(error ?? "Failed to update user status") });
        }


        /// <summary>Admin only, otherwise a user manager could grant themselves anything.</summary>
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


        private const string OwnAccountMessage = "You cannot delete or deactivate your own account.";

        /// <summary>Nobody switches their own account off, since only someone else could switch it on again.</summary>
        private async Task<(bool Success, string? Error)> ToggleStatusAsync(int id)
            => id == GetCurrentUserId()
                ? (false, OwnAccountMessage)
                : await _userManagementService.ToggleUserStatusAsync(id);

        /// <summary>Non-admins may not touch Admins or anyone holding a permission they lack, since a reset would hand it over.</summary>
        private async Task<bool> IsProtectedAsync(int userId)
        {
            if (User.IsInRole(AllRoles.Admin))
                return false;
            var target = await _identity.GetUserAsync(userId);
            return target != null
                && (target.Roles.Contains(AllRoles.Admin) || target.Permissions.Any(p => !User.HasPermission(p)));
        }

        private IActionResult Forbidden() => IsAjaxRequest()
            ? StatusCode(StatusCodes.Status403Forbidden, new { isSuccess = false, success = false, message = Tr("Only an administrator can change this account: it is an administrator or holds permissions you do not have.") })
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
