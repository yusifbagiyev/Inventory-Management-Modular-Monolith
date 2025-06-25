using System.Security.Claims;
using IdentityService.Application.DTOs;
using IdentityService.Application.Services;
using IdentityService.Domain.Constants;
using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace IdentityService.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ITokenService _tokenService;
        private readonly IdentityDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            ITokenService tokenService,
            IdentityDbContext context,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _context = context;
            _configuration = configuration;
        }

        public async Task<TokenDto> LoginAsync(LoginDto dto)
        {
            var user = await _userManager.FindByNameAsync(dto.Username);
            if (user == null || !user.IsActive)
                throw new UnauthorizedAccessException("Invalid credentials");

            var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password,false);
            if (!result.Succeeded)
                throw new UnauthorizedAccessException("Invalid credentials");

            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            // Revoke old refresh tokens
            await RevokeAllUserRefreshTokensAsync(user.Id);

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

        public async Task<TokenDto> RefreshTokenAsync(RefreshTokenDto dto)
        {
            //Validate the refresh token
            var refreshToken = await _tokenService.GetRefreshTokenAsync(dto.RefreshToken);
            if(refreshToken == null || !refreshToken.IsActive)
                throw new UnauthorizedAccessException("Invalid refresh token");

            //Get user from the refresh token
            var user=refreshToken.User;
            if(!user.IsActive)
                throw new UnauthorizedAccessException("User is inactive");

            //Get the principal from the expired token
            ClaimsPrincipal principal;
            try
            {
                principal=_tokenService.GetPrincipalFromExpiredToken(dto.AccessToken);
            }
            catch
            {
                throw new UnauthorizedAccessException("Invalid access token");
            }

            // Verify the user from the access token matches the refresh token user
            var userIdFromToken=principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdFromToken != user.Id.ToString())
                throw new UnauthorizedAccessException("Token mismatch");

            // Generate new tokens
            var newAccessToken = await _tokenService.GenerateAccessToken(user);
            var newRefreshToken = await _tokenService.GenerateRefreshToken();

            // Revoke old refresh token and create new one
            await _tokenService.RevokeRefreshTokenAsync(dto.RefreshToken, newRefreshToken);
            await _tokenService.CreateRefreshTokenAsync(user.Id, newRefreshToken);

            var userDto=await GetUserAsync(user.Id);

            return new TokenDto
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(Convert.ToDouble(_configuration["Jwt:ExpirationInMinutes"] ?? "20")),
                User = userDto
            };
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

            // Save the refresh token to database
            await _tokenService.CreateRefreshTokenAsync(user.Id, refreshToken);

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

        private async Task RevokeAllUserRefreshTokensAsync(int userId)
        {
            var activeTokens= await _context.RefreshTokens
                .Where(rt => rt.UserId == userId && !rt.IsRevoked)
                .ToListAsync();

            foreach(var token in activeTokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
            }

            if(activeTokens.Any())
                await _context.SaveChangesAsync();
        }
    }
}