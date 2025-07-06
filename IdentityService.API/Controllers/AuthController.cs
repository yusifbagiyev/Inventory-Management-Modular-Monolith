using IdentityService.Application.DTOs;
using IdentityService.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<ActionResult<TokenDto>> Login(LoginDto dto)
        {
            var result = await _authService.LoginAsync(dto);
            return Ok(result);
        }

        [HttpPost("register-by-admin")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<TokenDto>> RegisterByAdmin(RegisterDto dto)
        {
            var result = await _authService.RegisterAsync(dto);
            return Ok(result);
        }

        [HttpPost("refresh")]
        public async Task<ActionResult<TokenDto>> RefreshToken(RefreshTokenDto dto)
        {
            var result = await _authService.RefreshTokenAsync(dto);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<UserDto>> GetCurrentUser()
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            var user = await _authService.GetUserAsync(userId);
            return Ok(user);
        }

        [HttpGet("users")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetAllUsers()
        {
            var users = await _authService.GetAllUsersAsync();
            return Ok(users);
        }

        [HttpGet("users/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<UserDto>> GetUser(int id)
        {
            var user = await _authService.GetUserAsync(id);
            if (user == null)
                return NotFound();
            return Ok(user);
        }

        [HttpPut("users/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateUser(int id, UpdateUserDto dto)
        {
            if (id != dto.Id)
                return BadRequest();

            var result = await _authService.UpdateUserAsync(dto);
            if (!result)
                return BadRequest();

            return NoContent();
        }

        [HttpDelete("users/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var result = await _authService.DeleteUserAsync(id);
            if (!result)
                return BadRequest();

            return NoContent();
        }

        [HttpPost("users/{id}/toggle-status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ToggleUserStatus(int id)
        {
            var result = await _authService.ToggleUserStatusAsync(id);
            if (!result)
                return BadRequest();

            return NoContent();
        }

        [HttpPost("users/{id}/reset-password")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ResetPassword(int id, ResetPasswordDto dto)
        {
            var result = await _authService.ResetPasswordAsync(id, dto.NewPassword);
            if (!result)
                return BadRequest();

            return NoContent();
        }

        [HttpGet("roles")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<string>>> GetRoles()
        {
            var roles = await _authService.GetAllRolesAsync();
            return Ok(roles);
        }

        [HttpGet("permissions")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<object>>> GetPermissions()
        {
            var permissions = await _authService.GetAllPermissionsAsync();
            return Ok(permissions);
        }
    }
}