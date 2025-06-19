using IdentityService.Application.DTOs;
using IdentityService.Application.Services;
using IdentityService.Domain.Constants;
using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ITokenService _tokenService;
        private readonly IdentityDbContext _context;

        public AuthService(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            ITokenService tokenService,
            IdentityDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _context = context;
        }

        public async Task<TokenDto> LoginAsync(LoginDto dto)
        {
            var user = await _userManager.FindByNameAsync(dto.Username);
            if (user == null || !user.IsActive)
                throw new UnauthorizedAccessException("Invalid credentials");

            var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, false);
            if (!result.Succeeded)
                throw new UnauthorizedAccessException("Invalid credentials");

            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            return await GenerateTokenResponse(user);
        }

        public async Task<TokenDto> RegisterAsync(RegisterDto dto)
        {
            var user = new User
            {
                UserName = dto.Username,
                Email = dto.Email,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));

            // Assign default role
            var role = dto.Role ?? AllRoles.User;
            await _userManager.AddToRoleAsync(user, role);

            return await GenerateTokenResponse(user);
        }

        public Task<TokenDto> RefreshTokenAsync(RefreshTokenDto dto)
        {
            // Implement refresh token logic
            // This would validate the refresh token and generate new tokens
            throw new NotImplementedException();
        }

        public async Task<bool> HasPermissionAsync(int userId, string permission)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null || !user.IsActive)
                return false;

            var userRoles = await _userManager.GetRolesAsync(user);

            var hasPermission = await _context.RolePermissions
                .Include(rp => rp.Role)
                .Include(rp => rp.Permission)
                .AnyAsync(rp => userRoles.Contains(rp.Role.Name!) && rp.Permission.Name == permission);

            return hasPermission;
        }

        public async Task<UserDto> GetUserAsync(int userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString()) 
                ?? throw new KeyNotFoundException($"User with ID {userId} not found");

            var roles = await _userManager.GetRolesAsync(user);
            var permissions = await GetUserPermissionsAsync(userId, roles);

            return new UserDto
            {
                Id = user.Id,
                Username = user.UserName!,
                Email = user.Email!,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Roles = roles.ToList(),
                Permissions = permissions
            };
        }

        private async Task<TokenDto> GenerateTokenResponse(User user)
        {
            var accessToken = await _tokenService.GenerateAccessToken(user);
            var refreshToken = await _tokenService.GenerateRefreshToken();

            var userDto = await GetUserAsync(user.Id);

            return new TokenDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(Convert.ToDouble(60)), // From configuration
                User = userDto
            };
        }

        private async Task<List<string>> GetUserPermissionsAsync(int userId, IList<string> roles)
        {
            var permissions = await _context.RolePermissions
                .Include(rp => rp.Permission)
                .Where(rp => roles.Contains(rp.Role.Name!))
                .Select(rp => rp.Permission.Name)
                .Distinct()
                .ToListAsync();

            return permissions;
        }
    }
}
