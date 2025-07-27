using Microsoft.AspNetCore.Identity;

namespace IdentityService.Domain.Entities
{
    public class User : IdentityUser<int>
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public ICollection<UserPermission>? UserPermissions { get; set; }
    }
}