using IdentityService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using SharedServices.Contracts;

namespace IdentityService.Infrastructure.Services
{
    public class UserDirectory : IUserDirectory
    {
        private readonly IdentityDbContext _context;

        public UserDirectory(IdentityDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<int>> GetActiveUserIdsAsync(CancellationToken cancellationToken = default)
            => await _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive && _context.UserRoles.Any(ur => ur.UserId == u.Id))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<int>> GetActiveUserIdsInRoleAsync(string role, CancellationToken cancellationToken = default)
        {
            var normalizedRole = role.ToUpperInvariant();
            return await (from u in _context.Users.AsNoTracking()
                          join ur in _context.UserRoles on u.Id equals ur.UserId
                          join r in _context.Roles on ur.RoleId equals r.Id
                          where u.IsActive && r.NormalizedName == normalizedRole
                          select u.Id)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<int>> GetActiveUserIdsWithPermissionAsync(string permission, CancellationToken cancellationToken = default)
        {
            const string admin = "ADMIN";
            return await _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive && (
                    _context.UserRoles.Any(ur => ur.UserId == u.Id &&
                        _context.Roles.Any(r => r.Id == ur.RoleId && (r.NormalizedName == admin ||
                            _context.RolePermissions.Any(rp => rp.RoleId == r.Id && rp.Permission.Name == permission))))
                    || _context.UserPermissions.Any(up => up.UserId == u.Id && up.Permission.Name == permission)))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);
        }
    }
}
