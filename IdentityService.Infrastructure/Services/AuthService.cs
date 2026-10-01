using System.Security.Claims;
using IdentityService.Application.DTOs;
using IdentityService.Application.Services;
using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedServices.Identity;

namespace IdentityService.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<Role> _roleManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ITokenService _tokenService;
        private readonly IdentityDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            UserManager<User> userManager,
            RoleManager<Role> roleManager,
            SignInManager<User> signInManager,
            ITokenService tokenService,
            IdentityDbContext context,
            IConfiguration configuration,
            ILogger<AuthService> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        #region Authentication Methods

        /// <summary>A hash of a random password, verified for unknown and locked accounts so they take as long as real ones.</summary>
        private static string? _dummyHash;

        public async Task<UserDto> ValidateCredentialsAsync(string username, string password)
        {
            // Every failure gives the same answer in about the same time - unknown user, inactive,
            // locked out or wrong password - so the response does not tell which usernames exist.
            // A lockout is only logged; the sign-in page says sign-in pauses after repeated failures.
            var user = await _userManager.FindByNameAsync(username);
            if (user == null || !user.IsActive)
            {
                SpendPasswordCheck(password);
                throw new UnauthorizedAccessException("Invalid credentials");
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
            if (result.IsLockedOut)
            {
                SpendPasswordCheck(password);   // a locked account answers without hashing
                _logger.LogWarning("Sign-in to locked-out account {Username}", user.UserName);
                throw new UnauthorizedAccessException("Invalid credentials");
            }
            if (!result.Succeeded)
                throw new UnauthorizedAccessException("Invalid credentials");

            user.LastLoginAt = DateTime.Now;
            await _userManager.UpdateAsync(user);

            return (await GetUserAsync(user.Id))!;
        }

        private void SpendPasswordCheck(string password)
        {
            _dummyHash ??= _userManager.PasswordHasher.HashPassword(new User(), Guid.NewGuid().ToString());
            _userManager.PasswordHasher.VerifyHashedPassword(new User(), _dummyHash, password);
        }

        public async Task<string?> GetSessionStampAsync(int userId)
        {
            var stamp = await _userManager.Users.AsNoTracking()
                .Where(u => u.Id == userId && u.IsActive)
                .Select(u => u.SecurityStamp)
                .FirstOrDefaultAsync();
            return stamp == null ? null : SessionStamp(stamp);
        }

        /// <summary>
        /// What sessions carry instead of the security stamp itself. It changes with the stamp
        /// (password change or reset, deactivation), which ends the user's other sessions.
        /// </summary>
        internal static string SessionStamp(string securityStamp)
            => Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(securityStamp)), 0, 16);

        public async Task<TokenDto> LoginAsync(LoginDto dto)
        {
            var userDto = await ValidateCredentialsAsync(dto.Username, dto.Password);
            var user = (await _userManager.FindByIdAsync(userDto.Id.ToString()))!;

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
                CreatedAt = DateTime.Now,
                IsActive= true
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));

            // Assign role - validate against existing roles so a caller cannot inject an arbitrary role string
            var role = dto.SelectedRole ?? AllRoles.User;
            if (!await _roleManager.RoleExistsAsync(role))
                throw new InvalidOperationException($"Invalid role: {role}");
            await _userManager.AddToRoleAsync(user, role);

            return await GenerateTokenResponse(user);
        }

        public async Task<TokenDto> RefreshTokenAsync(RefreshTokenDto dto)
        {
            // Validate the refresh token first - this is the primary authentication proof
            var refreshToken = await _tokenService.GetRefreshTokenAsync(dto.RefreshToken);
            if (refreshToken == null)
                throw new UnauthorizedAccessException("Invalid refresh token");
            if (!refreshToken.IsActive)
            {
                // A token that was already exchanged is used again: it was copied. End all of that
                // user's tokens, the copier's included.
                if (refreshToken.ReplacedByToken != null)
                {
                    _logger.LogWarning("Reused refresh token for user {UserId}; all their tokens are revoked", refreshToken.UserId);
                    await RevokeAllUserRefreshTokensAsync(refreshToken.UserId);
                }
                throw new UnauthorizedAccessException("Invalid refresh token");
            }

            // Get user from the refresh token
            var user = refreshToken.User;
            if (!user.IsActive)
                throw new UnauthorizedAccessException("User is inactive");

            // If an access token was provided, we can optionally validate it for extra security
            // But we don't require it - the refresh token alone is sufficient proof of identity
            if (!string.IsNullOrEmpty(dto.AccessToken))
            {
                string? tokenUserId = null;
                try
                {
                    var principal = _tokenService.GetPrincipalFromExpiredToken(dto.AccessToken);
                    tokenUserId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                }
                catch (Exception ex)
                {
                    // An unreadable access token is ignored: the refresh token is the proof.
                    _logger?.LogWarning(ex, "Access token validation failed during refresh, but continuing with valid refresh token");
                }

                // Someone else's access token next to this refresh token: refuse.
                if (int.TryParse(tokenUserId, out var parsedUserId) && parsedUserId != user.Id)
                    throw new UnauthorizedAccessException("Token user mismatch");
            }
            else
            {
                // This is the session restoration scenario
                _logger?.LogInformation("Refresh token request without access token - restoring lost session for user {UserId}", user.Id);
            }

            // Generate new tokens
            var newAccessToken = await _tokenService.GenerateAccessToken(user);
            var newRefreshToken = await _tokenService.GenerateRefreshToken();

            // Revoke old refresh token and create new one (token rotation for security)
            await _tokenService.RevokeRefreshTokenAsync(dto.RefreshToken, newRefreshToken);
            await _tokenService.CreateRefreshTokenAsync(user.Id, newRefreshToken);

            var userDto = await GetUserAsync(user.Id);

            return new TokenDto
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = DateTime.Now.AddMinutes(Convert.ToDouble(_configuration["Jwt:ExpirationInMinutes"] ?? "60")),
                User = userDto!
            };
        }

        public async Task LogoutAsync(string refreshToken)
        {
            await _tokenService.RevokeRefreshTokenAsync(refreshToken);
        }

        #endregion

        #region User Management Methods

        public async Task<UserDto?> GetUserAsync(int userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return null;

            var roles = await _userManager.GetRolesAsync(user);
            var permissions = await GetUserPermissionsAsync(userId, roles);

            return new UserDto
            {
                Id = user.Id,
                Username = user.UserName!,
                Email = user.Email!,
                FirstName = user.FirstName,
                LastName = user.LastName,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                Roles = roles.ToList(),
                Permissions = permissions,
            };
        }

        public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
        {
            // Four queries in total. The previous version looped over every user and issued
            // GetRolesAsync + two permission queries each (3N+1), which NotificationService then
            // triggered three times per published event.
            var users = await _userManager.Users.AsNoTracking().ToListAsync();

            var rolesByUser = (await (from ur in _context.UserRoles
                                      join r in _context.Roles on ur.RoleId equals r.Id
                                      select new { ur.UserId, RoleName = r.Name! })
                                     .AsNoTracking()
                                     .ToListAsync())
                              .GroupBy(x => x.UserId)
                              .ToDictionary(g => g.Key, g => g.Select(x => x.RoleName).ToList());

            var permissionsByRole = (await _context.RolePermissions
                                          .AsNoTracking()
                                          .Select(rp => new { RoleName = rp.Role.Name!, PermissionName = rp.Permission.Name })
                                          .ToListAsync())
                                    .GroupBy(x => x.RoleName)
                                    .ToDictionary(g => g.Key, g => g.Select(x => x.PermissionName).ToList());

            var permissionsByUser = (await _context.UserPermissions
                                          .AsNoTracking()
                                          .Select(up => new { up.UserId, PermissionName = up.Permission.Name })
                                          .ToListAsync())
                                    .GroupBy(x => x.UserId)
                                    .ToDictionary(g => g.Key, g => g.Select(x => x.PermissionName).ToList());

            return users.Select(user =>
            {
                var roles = rolesByUser.TryGetValue(user.Id, out var userRoleNames)
                    ? userRoleNames
                    : new List<string>();

                var permissions = roles.SelectMany(role =>
                    permissionsByRole.TryGetValue(role, out var rolePerms)
                        ? rolePerms
                        : Enumerable.Empty<string>());

                if (permissionsByUser.TryGetValue(user.Id, out var directPerms))
                    permissions = permissions.Concat(directPerms);

                return new UserDto
                {
                    Id = user.Id,
                    Username = user.UserName!,
                    Email = user.Email!,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt,
                    LastLoginAt = user.LastLoginAt,
                    Roles = roles,
                    Permissions = permissions.Distinct().ToList()
                };
            }).ToList();
        }

        public async Task<bool> UpdateUserAsync(UpdateUserDto dto)
        {
            var user = await _userManager.FindByIdAsync(dto.Id.ToString());
            if (user == null)
                return false;

            user.UserName = dto.Username;
            user.Email = dto.Email;
            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;

            if (dto.IsActive.HasValue)
                user.IsActive = dto.IsActive.Value;

            var result = await _userManager.UpdateAsync(user);
            return result.Succeeded;
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return false;

            // Soft delete by deactivating the user
            var result = await _userManager.DeleteAsync(user);

            // Also revoke all refresh tokens
            await RevokeAllUserRefreshTokensAsync(userId);

            return result.Succeeded;
        }

        public async Task<bool> ToggleUserStatusAsync(int userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return false;

            user.IsActive = !user.IsActive;
            var result = await _userManager.UpdateAsync(user);
            if (!user.IsActive)
                await _userManager.UpdateSecurityStampAsync(user);

            // If user is deactivated, revoke all refresh tokens
            if (!user.IsActive)
            {
                await RevokeAllUserRefreshTokensAsync(userId);
            }

            return result.Succeeded;
        }

        #endregion

        #region Password Management

        public async Task<bool> ResetPasswordAsync(int userId, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return false;

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            // Revoke all refresh tokens after password reset
            if (result.Succeeded)
            {
                await RevokeAllUserRefreshTokensAsync(userId);
            }

            return result.Succeeded;
        }

        public async Task<(bool Succeeded, string? Error)> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return (false, "User not found");

            // A wrong current password counts toward the lockout, as at sign-in, so a session left
            // open cannot be used to guess the password.
            var check = await _signInManager.CheckPasswordSignInAsync(user, currentPassword, lockoutOnFailure: true);
            if (!check.Succeeded)
                return (false, check.IsLockedOut
                    ? "Too many wrong attempts. Try again in 15 minutes."
                    : "The current password is incorrect.");

            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (!result.Succeeded)
                return (false, result.Errors.FirstOrDefault()?.Description ?? "Could not change the password.");

            // Revoke all refresh tokens after password change
            await RevokeAllUserRefreshTokensAsync(userId);
            return (true, null);
        }

        #endregion

        #region Role Management

        public async Task<IEnumerable<string>> GetAllRolesAsync()
        {
            var roles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
            return roles;
        }

        public async Task<bool> AssignRoleAsync(int userId, string roleName)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return false;

            var roleExists = await _roleManager.RoleExistsAsync(roleName);
            if (!roleExists)
                return false;

            // Check if user already has this role
            var userRoles = await _userManager.GetRolesAsync(user);
            if (userRoles.Contains(roleName))
                return true; // Already has the role

            var result = await _userManager.AddToRoleAsync(user, roleName);
            return result.Succeeded;
        }

        public async Task<bool> SetRolesAsync(int userId, IEnumerable<string> roleNames)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return false;

            var wanted = roleNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var role in wanted)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                    return false;
            }

            var current = await _userManager.GetRolesAsync(user);
            var toRemove = current.Except(wanted, StringComparer.OrdinalIgnoreCase).ToList();
            var toAdd = wanted.Except(current, StringComparer.OrdinalIgnoreCase).ToList();

            if (toRemove.Count > 0 && !(await _userManager.RemoveFromRolesAsync(user, toRemove)).Succeeded)
                return false;
            if (toAdd.Count > 0 && !(await _userManager.AddToRolesAsync(user, toAdd)).Succeeded)
                return false;

            return true;
        }

        public async Task<bool> RemoveRoleAsync(int userId, string roleName)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return false;

            var result = await _userManager.RemoveFromRoleAsync(user, roleName);
            return result.Succeeded;
        }

        #endregion

        #region Permission Management

        public async Task<IReadOnlyList<PermissionDto>> GetAllPermissionsAsync()
            => await _context.Permissions
                .AsNoTracking()
                .OrderBy(p => p.Category).ThenBy(p => p.Name)
                .Select(p => new PermissionDto { Id = p.Id, Name = p.Name, Category = p.Category, Description = p.Description })
                .ToListAsync();

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

        public async Task<bool> GrantPermissionToUserAsync(int userId, string permissionName, string grantedBy)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return false;

            var permission = await _context.Permissions
                .FirstOrDefaultAsync(p => p.Name == permissionName);
            if (permission == null) return false;

            var existingPermission = await _context.UserPermissions
                .FirstOrDefaultAsync(up => up.UserId == userId && up.PermissionId == permission.Id);

            if (existingPermission != null) return true;

            _context.UserPermissions.Add(new UserPermission
            {
                UserId = userId,
                PermissionId = permission.Id,
                GrantedAt = DateTime.Now,
                GrantedBy = grantedBy
            });

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RevokePermissionFromUserAsync(int userId, string permissionName)
        {
            var permission = await _context.Permissions
                .FirstOrDefaultAsync(p => p.Name == permissionName);
            if (permission == null) return false;

            var userPermission = await _context.UserPermissions
                .FirstOrDefaultAsync(up => up.UserId == userId && up.PermissionId == permission.Id);

            if (userPermission == null) return true;

            _context.UserPermissions.Remove(userPermission);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<PermissionDto>> GetUserDirectPermissionsAsync(int userId)
        {
            var permissions = await _context.UserPermissions
                .Include(up => up.Permission)
                .Where(up => up.UserId == userId)
                .Select(up => new PermissionDto
                {
                    Id = up.Permission.Id,
                    Name = up.Permission.Name,
                    Description = up.Permission.Description,
                    Category = up.Permission.Category
                })
                .ToListAsync();

            return permissions;
        }

        public async Task<IReadOnlyList<string>> GetRolePermissionsAsync(string roleName)
            => await _context.RolePermissions
                .Where(rp => rp.Role.Name == roleName)
                .Select(rp => rp.Permission.Name)
                .ToListAsync();

        public async Task<bool> SetRolePermissionAsync(string roleName, string permissionName, bool grant)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
            var permission = await _context.Permissions.FirstOrDefaultAsync(p => p.Name == permissionName);
            if (role == null || permission == null) return false;

            var existing = await _context.RolePermissions
                .FirstOrDefaultAsync(rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id);
            if (grant == (existing != null)) return true;

            if (grant)
                _context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id, Role = role, Permission = permission });
            else
                _context.RolePermissions.Remove(existing!);
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<List<string>> GetUserPermissionsAsync(int userId, IList<string> roles)
        {
            var rolePermissions = await _context.RolePermissions
                .Include(rp => rp.Permission)
                .Where(rp => roles.Contains(rp.Role.Name!))
                .Select(rp => rp.Permission.Name)
                .ToListAsync();

            var userPermissions = await _context.UserPermissions
                .Include(up => up.Permission)
                .Where(up => up.UserId == userId)
                .Select(up => up.Permission.Name)
                .ToListAsync();

            return rolePermissions.Union(userPermissions).Distinct().ToList();
        }

        #endregion


        #region Private Helper Methods

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
                ExpiresAt = DateTime.Now.AddMinutes(Convert.ToDouble(_configuration["Jwt:ExpirationInMinutes"] ?? "60")),
                User = userDto!
            };
        }

        private async Task RevokeAllUserRefreshTokensAsync(int userId)
        {
            var activeTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId && !rt.IsRevoked)
                .ToListAsync();

            foreach (var token in activeTokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.Now;
            }

            if (activeTokens.Any())
                await _context.SaveChangesAsync();
        }

        #endregion
    }
}