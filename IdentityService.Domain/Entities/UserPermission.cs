namespace IdentityService.Domain.Entities
{
    /// <summary>A permission granted to one user directly, on top of what their role gives.</summary>
    public class UserPermission
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public int PermissionId { get; set; }
        public Permission Permission { get; set; } = null!;
        public DateTime GrantedAt { get; set; }
        public string GrantedBy { get; set; } = string.Empty;
    }
}