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

        public async Task<IReadOnlyList<int>> GetActiveUserIdsWithPermissionAsync(string permission, CancellationToken cancellationToken = default)
        {
            // Admins count as holding every permission without any rows of their own
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
