using Microsoft.AspNetCore.Identity;

namespace IdentityService.Domain.Entities
{
    public class Role:IdentityRole<int>
    {
        public string Description { get; set; } = string.Empty;
        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}
