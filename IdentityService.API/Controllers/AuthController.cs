using IdentityService.Application.DTOs;
using IdentityService.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace IdentityService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }


        [HttpPost("login")]
        [EnableRateLimiting("LoginPolicyPerIP")]
        public async Task<ActionResult<TokenDto>> Login(LoginDto dto)
        {
            var ipAddress = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim()
                ?? HttpContext.Request.Headers["X-Real-IP"].FirstOrDefault()
                ?? HttpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown";

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

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(
                    "Login failed for user {Username} from IP {IpAddress}: {Reason}",
                    dto.Username,
                    ipAddress,
                    ex.Message);

                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Login error for user {Username} from IP {IpAddress}",
                    dto.Username,
                    ipAddress);

                return BadRequest(new { message = ex.Message });
            }
        }


        [HttpPost("register-by-admin")]
        public async Task<ActionResult<TokenDto>> RegisterByAdmin(RegisterDto dto)
        {
            try
            {
                var result = await _authService.RegisterAsync(dto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


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
                return BadRequest(new { message = ex.Message });
            }
        }


        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<UserDto>> GetCurrentUser()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                if (userId == 0)
                    return Unauthorized(new { message = "Invalid user token" });

                var user = await _authService.GetUserAsync(userId);
                if (user == null)
                    return NotFound(new { message = "User not found" });

                return Ok(user);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


        [HttpGet("users")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetAllUsers()
        {
            try
            {
                var users = await _authService.GetAllUsersAsync();
                return Ok(users);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


        [HttpGet("users/{id}")]
        [Authorize(Roles = "Admin")]
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
                return BadRequest(new { message = ex.Message });
            }
        }


        [HttpPut("users/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateUser(int id, UpdateUserDto dto)
        {
            try
            {
                if (id != dto.Id)
                    return BadRequest(new { message = "User ID mismatch" });

                var result = await _authService.UpdateUserAsync(dto);
                if (!result)
                    return BadRequest(new { message = "Failed to update user" });

                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


        [HttpDelete("users/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                var result = await _authService.DeleteUserAsync(id);
                if (!result)
                    return BadRequest(new { message = "Failed to delete user" });

                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


        [HttpPost("users/{id}/toggle-status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ToggleUserStatus(int id)
        {
            try
            {
                var result = await _authService.ToggleUserStatusAsync(id);
                if (!result)
                    return BadRequest(new { message = "Failed to toggle user status" });

                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


        [HttpPost("users/{id}/reset-password")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ResetPassword(int id, ResetPasswordDto dto)
        {
            try
            {
                var result = await _authService.ResetPasswordAsync(id, dto.NewPassword);
                if (!result)
                    return BadRequest(new { message = "Failed to reset password" });

                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
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
                return BadRequest(new { message = ex.Message });
            }
        }


        [HttpGet("permissions")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<object>>> GetPermissions()
        {
            try
            {
                var permissions = await _authService.GetAllPermissionsAsync();
                return Ok(permissions);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
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
                return BadRequest(new { message = ex.Message });
            }
        }


        [HttpPost("users/{id}/remove-role")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemoveRole(int id, RemoveRoleDto dto)
        {
            try
            {
                var result = await _authService.RemoveRoleAsync(id, dto.RoleName);
                if (!result)
                    return BadRequest(new { message = "Failed to remove role" });

                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
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
                return BadRequest(new { message = ex.Message });
            }
        }


        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout(LogoutDto dto)
        {
            try
            {
                await _authService.LogoutAsync(dto.RefreshToken);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                if (userId == 0)
                    return Unauthorized(new { message = "Invalid user token" });

                var result = await _authService.ChangePasswordAsync(userId, dto.CurrentPassword, dto.NewPassword);
                if (!result)
                    return BadRequest(new { message = "Failed to change password" });

                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
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
                return BadRequest(new { message = ex.Message });
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
                return BadRequest(new { message = ex.Message });
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
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("users/by-role/{role}")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetUsersByRole(string role)
        {
            try
            {
                var users = await _authService.GetAllUsersAsync();
                var usersInRole = users.Where(u => u.Roles.Contains(role));
                return Ok(usersInRole);
            }
            catch 
            {
                return BadRequest();
            }
        }
    }
}