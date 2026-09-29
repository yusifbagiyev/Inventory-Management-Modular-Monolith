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
    }
}
