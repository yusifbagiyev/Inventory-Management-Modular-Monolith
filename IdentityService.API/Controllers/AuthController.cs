using IdentityService.Application.DTOs;
using IdentityService.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using SharedServices.Authorization;
using SharedServices.Identity;
using SharedServices.Web;

namespace IdentityService.API.Controllers
{
    /// <summary>JWT sign-in and user administration for external API clients.</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;
        private readonly LoginThrottle _throttle;
        private readonly SessionAudit _sessionAudit;

        public AuthController(IAuthService authService, ILogger<AuthController> logger, LoginThrottle throttle, SessionAudit sessionAudit)
        {
            _authService = authService;
            _logger = logger;
            _throttle = throttle;
            _sessionAudit = sessionAudit;
        }


        [AllowAnonymous]
        [HttpPost("login")]
        [EnableRateLimiting(IdentityModule.LoginRateLimitPolicy)]
        public async Task<ActionResult<TokenDto>> Login(LoginDto dto)
        {
            // RemoteIpAddress already comes from the trusted proxy, while X-Forwarded-For is client-controlled
            var address = HttpContext.Connection.RemoteIpAddress;
            var ipAddress = address?.ToString() ?? "unknown";
            if (_throttle.RetryAfter(address) is { } wait)
            {
                Response.Headers.RetryAfter = ((int)Math.Ceiling(wait.TotalSeconds)).ToString();
                return StatusCode(StatusCodes.Status429TooManyRequests, new { message = "Too many failed sign-ins. Try again later." });
            }

            _logger.LogInformation(
                "Login attempt for user {Username} from IP {IpAddress}",
                dto.Username,
                ipAddress);

            try
            {
                var result = await _authService.LoginAsync(dto);

                _logger.LogInformation(
                    "Login successful for user {Username} from IP {IpAddress}",
                    dto.Username,
                    ipAddress);
                // API sign-ins show in the audit log next to those made on the sign-in page
                await _sessionAudit.SignedInAsync(result.User);

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _throttle.RecordFailure(address);
                _logger.LogWarning(
                    "Login failed for user {Username} from IP {IpAddress}: {Reason}",
                    dto.Username,
                    ipAddress,
                    ex.Message);
                await _sessionAudit.SignInFailedAsync(dto.Username, ex.Message);

                return Unauthorized(new { message = "Invalid credentials" });
            }
            catch (Exception ex)
            {
                // Anyone can call this, so internal error text stays in the log
                _logger.LogError(ex,
                    "Login error for user {Username} from IP {IpAddress}",
                    dto.Username,
                    ipAddress);

                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Sign-in failed. Try again later." });
            }
        }


        [Permission(AllPermissions.UserManage)]
        [HttpPost("register-by-admin")]
        public async Task<ActionResult<TokenDto>> RegisterByAdmin(RegisterDto dto)
        {
            try
            {
                // Only Admins choose the role, same as on the Users page
                if (!User.IsInRole(AllRoles.Admin))
                    dto = dto with { SelectedRole = AllRoles.User };
                var result = await _authService.RegisterAsync(dto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return Failure(ex);
            }
        }


        [AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<ActionResult<TokenDto>> RefreshToken(RefreshTokenDto dto)
        {
            try
            {
                var result = await _authService.RefreshTokenAsync(dto);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return Failure(ex);
            }
        }


        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<UserDto>> GetCurrentUser()
        {
            try
            {
                var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsedId) ? parsedId : 0;
                if (userId == 0)
                    return Unauthorized(new { message = "Invalid user token" });

                var user = await _authService.GetUserAsync(userId);
                if (user == null)
                    return NotFound(new { message = "User not found" });

                return Ok(user);
            }
            catch (Exception ex)
            {
                return Failure(ex);
            }
        }


        [HttpGet("users")]
        [Permission(AllPermissions.UserView)]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetAllUsers()
        {
            try
            {
                var users = await _authService.GetAllUsersAsync();
                return Ok(users);
            }
            catch (Exception ex)
            {
                return Failure(ex);
            }
        }


        [HttpGet("users/{id}")]
        [Permission(AllPermissions.UserView)]
        public async Task<ActionResult<UserDto>> GetUser(int id)
        {
            try
            {
                var user = await _authService.GetUserAsync(id);
                if (user == null)
                    return NotFound(new { message = "User not found" });
                return Ok(user);
            }
            catch (Exception ex)
            {
                return Failure(ex);
            }
        }


        [HttpPut("users/{id}")]
        [Permission(AllPermissions.UserManage)]
        public async Task<IActionResult> UpdateUser(int id, UpdateUserDto dto)
        {
            try
            {
                if (await IsProtectedAsync(id))
                    return Forbid();
                if (id != dto.Id)
                    return BadRequest(new { message = "User ID mismatch" });
                if (dto.IsActive == false && IsOwnAccount(id))
                    return BadRequest(new { message = OwnAccountMessage });

                var (succeeded, error) = await _authService.UpdateUserAsync(dto);
                if (!succeeded)
                    return BadRequest(new { message = error ?? "Failed to update user" });

                return NoContent();
            }
            catch (Exception ex)
            {
                return Failure(ex);
            }
        }


        [HttpDelete("users/{id}")]
        [Permission(AllPermissions.UserManage)]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                if (await IsProtectedAsync(id))
                    return Forbid();
                if (IsOwnAccount(id))
                    return BadRequest(new { message = OwnAccountMessage });

                var (succeeded, error) = await _authService.DeleteUserAsync(id);
                if (!succeeded)
                    return BadRequest(new { message = error ?? "Failed to delete user" });

                return NoContent();
            }
            catch (Exception ex)
            {
                return Failure(ex);
            }
        }


        [HttpPost("users/{id}/toggle-status")]
        [Permission(AllPermissions.UserManage)]
        public async Task<IActionResult> ToggleUserStatus(int id)
        {
            try
            {
                if (await IsProtectedAsync(id))
                    return Forbid();
                if (IsOwnAccount(id))
                    return BadRequest(new { message = OwnAccountMessage });

                var (succeeded, error) = await _authService.ToggleUserStatusAsync(id);
                if (!succeeded)
                    return BadRequest(new { message = error ?? "Failed to toggle user status" });

                return NoContent();
            }
            catch (Exception ex)
            {
                return Failure(ex);
            }
        }


        [HttpPost("users/{id}/reset-password")]
        [Permission(AllPermissions.UserManage)]
        public async Task<IActionResult> ResetPassword(int id, ResetPasswordDto dto)
        {
            try
            {
                if (await IsProtectedAsync(id))
                    return Forbid();
                var (succeeded, error) = await _authService.ResetPasswordAsync(id, dto.NewPassword);
                if (!succeeded)
                    return BadRequest(new { message = error ?? "Failed to reset password" });

                return NoContent();
            }
            catch (Exception ex)
            {
                return Failure(ex);
            }
        }


        [HttpGet("roles")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<string>>> GetRoles()
        {
            try
            {
                var roles = await _authService.GetAllRolesAsync();
                return Ok(roles);
            }
            catch (Exception ex)
            {
                return Failure(ex);
            }
        }


        [HttpGet("permissions")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<PermissionDto>>> GetPermissions()
        {
            try
            {
                var permissions = await _authService.GetAllPermissionsAsync();
                return Ok(permissions);
            }
            catch (Exception ex)
            {
                return Failure(ex);
            }
        }


        [HttpPost("users/{id}/assign-role")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignRole(int id, AssignRoleDto dto)
        {
            try
            {
                var result = await _authService.AssignRoleAsync(id, dto.RoleName);
                if (!result)
                    return BadRequest(new { message = "Failed to assign role" });

                return NoContent();
            }
            catch (Exception ex)
            {
                return Failure(ex);
            }
        }


        [HttpPost("users/{id}/remove-role")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemoveRole(int id, RemoveRoleDto dto)
        {
            try
            {
                var (succeeded, error) = await _authService.RemoveRoleAsync(id, dto.RoleName);
                if (!succeeded)
                    return BadRequest(new { message = error ?? "Failed to remove role" });

                return NoContent();
            }
            catch (Exception ex)
            {
                return Failure(ex);
            }
        }


        [HttpPost("users/{id}/check-permission")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<bool>> CheckPermission(int id, CheckPermissionDto dto)
        {
            try
            {
                var hasPermission = await _authService.HasPermissionAsync(id, dto.Permission);
                return Ok(new { hasPermission });
            }
            catch (Exception ex)
            {
                return Failure(ex);
            }
        }


        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout(LogoutDto dto)
        {
            try
            {
                await _authService.LogoutAsync(dto.RefreshToken);
                await _sessionAudit.SignedOutAsync(User.Identity?.Name);
                return NoContent();
            }
            catch (Exception ex)
            {
                return Failure(ex);
            }
        }


        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
        {
            try
            {
                var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsedId) ? parsedId : 0;
                if (userId == 0)
                    return Unauthorized(new { message = "Invalid user token" });

                var (succeeded, error) = await _authService.ChangePasswordAsync(userId, dto.CurrentPassword, dto.NewPassword);
                if (!succeeded)
                    return BadRequest(new { message = error });

                return NoContent();
            }
            catch (Exception ex)
            {
                return Failure(ex);
            }
        }


        [HttpPost("users/{id}/grant-permission")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GrantPermission(int id, [FromBody] GrantPermissionDto dto)
        {
            try
            {
                var grantedBy = User.Identity?.Name ?? "System";
                var result = await _authService.GrantPermissionToUserAsync(id, dto.PermissionName, grantedBy);
                if (!result)
                    return BadRequest(new { message = "Failed to grant permission" });

                return NoContent();
            }
            catch (Exception ex)
            {
                return Failure(ex);
            }
        }

        [HttpPost("users/{id}/revoke-permission")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RevokePermission(int id, [FromBody] RevokePermissionDto dto)
        {
            try
            {
                var result = await _authService.RevokePermissionFromUserAsync(id, dto.PermissionName);
                if (!result)
                    return BadRequest(new { message = "Failed to revoke permission" });

                return NoContent();
            }
            catch (Exception ex)
            {
                return Failure(ex);
            }
        }

        [HttpGet("users/{id}/direct-permissions")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<List<PermissionDto>>> GetUserDirectPermissions(int id)
        {
            try
            {
                var permissions = await _authService.GetUserDirectPermissionsAsync(id);
                return Ok(permissions);
            }
            catch (Exception ex)
            {
                return Failure(ex);
            }
        }

        /// <summary>A refused request keeps the module's own message, while any other error is logged and answered generically.</summary>
        private ObjectResult Failure(Exception exception, [CallerMemberName] string action = "")
        {
            if ((exception is InvalidOperationException or ArgumentException) && UserFacingErrors.IsUserFacing(exception))
            {
                _logger.LogInformation("Auth {Action} refused: {Message}", action, exception.Message);
                return BadRequest(new { message = exception.Message });
            }

            _logger.LogError(exception, "Auth {Action} failed", action);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = UserFacingErrors.Generic });
        }

        private const string OwnAccountMessage = "You cannot delete or deactivate your own account.";

        /// <summary>Nobody removes their own access by mistake, since only someone else could give it back.</summary>
        private bool IsOwnAccount(int userId)
            => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var callerId) && callerId == userId;

        /// <summary>True when only an Admin may change the account, being an Admin or holding a permission the caller lacks.</summary>
        private async Task<bool> IsProtectedAsync(int userId)
        {
            if (User.IsInRole(AllRoles.Admin)) return false;
            var target = await _authService.GetUserAsync(userId);
            var mine = User.FindAll("permission").Select(c => c.Value).ToHashSet();
            return target != null && (target.Roles.Contains(AllRoles.Admin) || target.Permissions.Any(p => !mine.Contains(p)));
        }
    }
}
