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
    /// <summary>Sign-in, tokens, users, roles and permissions on top of ASP.NET Core Identity.</summary>
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

        /// <summary>Hash of a random password, checked for unknown and locked accounts so they take as long as real ones.</summary>
        private static string? _dummyHash;

        public async Task<UserDto> ValidateCredentialsAsync(string username, string password)
        {
            // Every failure gives the same answer in about the same time, so nobody can probe for usernames
            var user = await _userManager.FindByNameAsync(username);
            if (user == null || !user.IsActive)
            {
                SpendPasswordCheck(password);
                throw new UnauthorizedAccessException("Invalid credentials");
            }

            if (IsLockedOut(user))
            {
                // A locked account answers without hashing, so spend the time here
                SpendPasswordCheck(password);
                _logger.LogWarning("Sign-in to locked-out account {Username}", user.UserName);
                throw new UnauthorizedAccessException("Invalid credentials");
            }

            // The failure that locks the account has hashed once like any other, so it gets no extra check
            var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
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

        /// <summary>Reads the lock itself, because Identity ignores it for accounts stored with LockoutEnabled off, as the seeded and imported ones are.</summary>
        private static bool IsLockedOut(User user) => user.LockoutEnd is { } until && until > DateTimeOffset.UtcNow;

        public async Task<string?> GetSessionStampAsync(int userId)
        {
            var stamp = await _userManager.Users.AsNoTracking()
                .Where(u => u.Id == userId && u.IsActive)
                .Select(u => u.SecurityStamp)
                .FirstOrDefaultAsync();
            return stamp == null ? null : SessionStamp(stamp);
        }

        /// <summary>What sessions carry instead of the security stamp, so a stamp change ends them.</summary>
        internal static string SessionStamp(string securityStamp)
            => Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(securityStamp)), 0, 16);

        public async Task<TokenDto> LoginAsync(LoginDto dto)
        {
            var userDto = await ValidateCredentialsAsync(dto.Username, dto.Password);
            var user = (await _userManager.FindByIdAsync(userDto.Id.ToString()))!;

            // A new API sign-in ends the user's older refresh tokens
            await RevokeAllUserRefreshTokensAsync(user.Id);

            return await GenerateTokenResponse(user);
        }

        public async Task<TokenDto> RegisterAsync(RegisterDto dto)
        {
            // Only existing roles, checked first so a wrong name does not leave an account without a role behind
            var role = dto.SelectedRole ?? AllRoles.User;
            if (!await _roleManager.RoleExistsAsync(role))
                throw new InvalidOperationException($"Invalid role: {role}");

            var user = new User
            {
                UserName = dto.Username,
                Email = dto.Email,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                CreatedAt = DateTime.Now,
                IsActive = dto.IsActive
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));

            var added = await _userManager.AddToRoleAsync(user, role);
            if (!added.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                throw new InvalidOperationException(string.Join(", ", added.Errors.Select(e => e.Description)));
            }

            // An account created inactive gets no tokens, since they would start working the day it is activated
            if (!user.IsActive)
                return new TokenDto { User = (await GetUserAsync(user.Id))! };

            return await GenerateTokenResponse(user);
        }

        public async Task<TokenDto> RefreshTokenAsync(RefreshTokenDto dto)
        {
            // The refresh token is the proof of identity here
            var refreshToken = await _tokenService.GetRefreshTokenAsync(dto.RefreshToken);
            if (refreshToken == null)
                throw new UnauthorizedAccessException("Invalid refresh token");
            if (!refreshToken.IsActive)
            {
                // An exchanged token used again was copied, so end all of the user's tokens
                if (refreshToken.ReplacedByToken != null)
                {
                    _logger.LogWarning("Reused refresh token for user {UserId}; all their tokens are revoked", refreshToken.UserId);
                    await RevokeAllUserRefreshTokensAsync(refreshToken.UserId);
                }
                throw new UnauthorizedAccessException("Invalid refresh token");
            }

            var user = refreshToken.User;
            if (!user.IsActive)
                throw new UnauthorizedAccessException("User is inactive");

            // The access token is optional, but when sent it must belong to the same user
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
                    // An unreadable access token is ignored since the refresh token is enough
                    _logger?.LogWarning(ex, "Access token validation failed during refresh, but continuing with valid refresh token");
                }

                // Refuse someone else's access token next to this refresh token
                if (int.TryParse(tokenUserId, out var parsedUserId) && parsedUserId != user.Id)
                    throw new UnauthorizedAccessException("Token user mismatch");
            }
            else
            {
                _logger?.LogInformation("Refresh token request without access token - restoring lost session for user {UserId}", user.Id);
            }

            var newAccessToken = await _tokenService.GenerateAccessToken(user);
            var newRefreshToken = await _tokenService.GenerateRefreshToken();

            // Rotate so the old token is marked as replaced and any reuse of it is caught
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

        private const string LastAdminMessage = "This is the only active administrator, so it cannot be deleted, deactivated or given another role.";

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
            // Four queries in total instead of several per user
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

        public async Task<(bool Succeeded, string? Error)> UpdateUserAsync(UpdateUserDto dto)
        {
            var user = await _userManager.FindByIdAsync(dto.Id.ToString());
            if (user == null)
                return (false, "User not found");

            var deactivating = user.IsActive && dto.IsActive == false;
            if (deactivating && await IsLastActiveAdminAsync(user))
                return (false, LastAdminMessage);

            user.UserName = dto.Username;
            user.Email = dto.Email;
            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;

            if (dto.IsActive.HasValue)
                user.IsActive = dto.IsActive.Value;

            var result = await _userManager.UpdateAsync(user);
            // Only on deactivation, or every profile edit would sign the user out
            if (result.Succeeded && deactivating)
                await EndSessionsAsync(user);

            return Outcome(result);
        }

        public async Task<(bool Succeeded, string? Error)> DeleteUserAsync(int userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return (false, "User not found");

            if (await IsLastActiveAdminAsync(user))
                return (false, LastAdminMessage);

            var result = await _userManager.DeleteAsync(user);

            await RevokeAllUserRefreshTokensAsync(userId);

            return Outcome(result);
        }

        public async Task<(bool Succeeded, string? Error)> ToggleUserStatusAsync(int userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return (false, "User not found");

            if (user.IsActive && await IsLastActiveAdminAsync(user))
                return (false, LastAdminMessage);

            user.IsActive = !user.IsActive;
            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded && !user.IsActive)
                await EndSessionsAsync(user);

            return Outcome(result);
        }

        /// <summary>Ends what was issued to a deactivated user, so none of it works again once the account is switched back on.</summary>
        private async Task EndSessionsAsync(User user)
        {
            // A new stamp ends the cookie sessions and access tokens
            await _userManager.UpdateSecurityStampAsync(user);
            await RevokeAllUserRefreshTokensAsync(user.Id);
        }

        /// <summary>Without another active Admin nobody could manage roles, permissions or Admin accounts any more.</summary>
        private async Task<bool> IsLastActiveAdminAsync(User user)
        {
            if (!user.IsActive || !await _userManager.IsInRoleAsync(user, AllRoles.Admin))
                return false;

            return !await (from userRole in _context.UserRoles
                           join role in _context.Roles on userRole.RoleId equals role.Id
                           join other in _context.Users on userRole.UserId equals other.Id
                           where role.Name == AllRoles.Admin && other.IsActive && other.Id != user.Id
                           select other.Id).AnyAsync();
        }

        private static (bool Succeeded, string? Error) Outcome(IdentityResult result)
            => result.Succeeded ? (true, null) : (false, string.Join(", ", result.Errors.Select(e => e.Description)));

        #endregion

        #region Password Management

        public async Task<(bool Succeeded, string? Error)> ResetPasswordAsync(int userId, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return (false, "User not found");

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            if (result.Succeeded)
            {
                // A locked-out user can sign in with the new password at once, without waiting for the lock to expire
                if (user.LockoutEnd != null || user.AccessFailedCount > 0)
                {
                    user.LockoutEnd = null;
                    user.AccessFailedCount = 0;
                    await _userManager.UpdateAsync(user);
                }

                await RevokeAllUserRefreshTokensAsync(userId);
            }

            return Outcome(result);
        }

        public async Task<(bool Succeeded, string? Error)> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return (false, "User not found");

            const string lockedOut = "Too many failed attempts. Please try again in 15 minutes.";
            if (IsLockedOut(user))
                return (false, lockedOut);

            // Counts toward lockout, so an unattended session cannot be used to guess the password
            var check = await _signInManager.CheckPasswordSignInAsync(user, currentPassword, lockoutOnFailure: true);
            if (!check.Succeeded)
                return (false, IsLockedOut(user) ? lockedOut : "The current password is incorrect.");

            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (!result.Succeeded)
                return (false, result.Errors.FirstOrDefault()?.Description ?? "Could not change the password.");

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

            var userRoles = await _userManager.GetRolesAsync(user);
            if (userRoles.Contains(roleName))
                return true;

            var result = await _userManager.AddToRoleAsync(user, roleName);
            return result.Succeeded;
        }

        public async Task<(bool Succeeded, string? Error)> SetRolesAsync(int userId, IEnumerable<string> roleNames)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return (false, "User not found");

            var wanted = roleNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var role in wanted)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                    return (false, $"Invalid role: {role}");
            }

            var current = await _userManager.GetRolesAsync(user);
            var toRemove = current.Except(wanted, StringComparer.OrdinalIgnoreCase).ToList();
            var toAdd = wanted.Except(current, StringComparer.OrdinalIgnoreCase).ToList();

            if (toRemove.Contains(AllRoles.Admin, StringComparer.OrdinalIgnoreCase) && await IsLastActiveAdminAsync(user))
                return (false, LastAdminMessage);

            if (toRemove.Count > 0)
            {
                var removed = await _userManager.RemoveFromRolesAsync(user, toRemove);
                if (!removed.Succeeded)
                    return Outcome(removed);
            }

            return toAdd.Count > 0 ? Outcome(await _userManager.AddToRolesAsync(user, toAdd)) : (true, null);
        }

        public async Task<(bool Succeeded, string? Error)> RemoveRoleAsync(int userId, string roleName)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return (false, "User not found");

            if (string.Equals(roleName, AllRoles.Admin, StringComparison.OrdinalIgnoreCase) && await IsLastActiveAdminAsync(user))
                return (false, LastAdminMessage);

            return Outcome(await _userManager.RemoveFromRoleAsync(user, roleName));
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

            // Answers as the real checks do, where an Admin passes everything and the user's own grants count next to the role's
            var userRoles = await _userManager.GetRolesAsync(user);
            return userRoles.Contains(AllRoles.Admin)
                || (await GetUserPermissionsAsync(userId, userRoles)).Contains(permission);
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