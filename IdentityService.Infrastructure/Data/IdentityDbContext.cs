using IdentityService.Domain.Constants;
using IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Data
{
    public class IdentityDbContext : IdentityDbContext<User, Role, int>
    {
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }

        public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure RolePermission many-to-many
            builder.Entity<RolePermission>()
                .HasKey(rp => new { rp.RoleId, rp.PermissionId });

            builder.Entity<RolePermission>()
                .HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId);

            builder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId);

            // Seed initial data
            SeedData(builder);
        }

        private void SeedData(ModelBuilder builder)
        {
            // Seed Permissions
            var permissions = new[]
            {
                new Permission { Id = 1, Name = AllPermissions.RouteView, Category = "Route", Description = "View routes" },
                new Permission { Id = 2, Name = AllPermissions.RouteCreate, Category = "Route", Description = "Create routes" },
                new Permission { Id = 3, Name = AllPermissions.RouteUpdate, Category = "Route", Description = "Update routes" },
                new Permission { Id = 4, Name = AllPermissions.RouteDelete, Category = "Route", Description = "Delete routes" },
                new Permission { Id = 5, Name = AllPermissions.RouteComplete, Category = "Route", Description = "Complete routes" },
                new Permission { Id = 6, Name = AllPermissions.RouteBatchDelete, Category = "Route", Description = "Batch delete routes" },
                new Permission { Id = 7, Name = AllPermissions.ProductView, Category = "Product", Description = "View products" },
                new Permission { Id = 8, Name = AllPermissions.ProductCreate, Category = "Product", Description = "Create products" },
                new Permission { Id = 9, Name = AllPermissions.ProductUpdate, Category = "Product", Description = "Update products" },
                new Permission { Id = 10, Name = AllPermissions.ProductDelete, Category = "Product", Description = "Delete products" },
                new Permission { Id = 11, Name = AllPermissions.ProductTransfer, Category = "Product", Description = "Transfer products" },
                new Permission { Id = 12, Name = AllPermissions.UserManage, Category = "Admin", Description = "Manage users" },
                new Permission { Id = 13, Name = AllPermissions.RoleManage, Category = "Admin", Description = "Manage roles" },
                new Permission { Id = 14, Name = AllPermissions.SystemConfig, Category = "Admin", Description = "System configuration" }
            };
            builder.Entity<Permission>().HasData(permissions);

            // Seed Roles
            var roles = new[]
            {
                new Role { Id = 1, Name = AllRoles.Admin, NormalizedName = AllRoles.Admin.ToUpper(), Description = "System Administrator" },
                new Role { Id = 2, Name = AllRoles.Manager, NormalizedName = AllRoles.Manager.ToUpper(), Description = "Department Manager" },
                new Role { Id = 3, Name = AllRoles.User, NormalizedName = AllRoles.User.ToUpper(), Description = "Regular User" },
                new Role { Id = 4, Name = AllRoles.Viewer, NormalizedName = AllRoles.Viewer.ToUpper(), Description = "Read-only Access" }
            };
            builder.Entity<Role>().HasData(roles);

            // Seed Role Permissions
            var rolePermissions = new List<RolePermission>();

            // Admin - All permissions
            for (int i = 1; i <= 14; i++)
            {
                rolePermissions.Add(new RolePermission { RoleId = 1, PermissionId = i });
            }

            // Manager - All route and product permissions
            for (int i = 1; i <= 11; i++)
            {
                rolePermissions.Add(new RolePermission { RoleId = 2, PermissionId = i });
            }

            // User - Basic permissions
            rolePermissions.AddRange(new[]
            {
                new RolePermission { RoleId = 3, PermissionId = 1 }, // RouteView
                new RolePermission { RoleId = 3, PermissionId = 2 }, // RouteCreate
                new RolePermission { RoleId = 3, PermissionId = 3 }, // RouteUpdate
                new RolePermission { RoleId = 3, PermissionId = 5 }, // RouteComplete
                new RolePermission { RoleId = 3, PermissionId = 7 }, // ProductView
                new RolePermission { RoleId = 3, PermissionId = 11 } // ProductTransfer
            });

            // Viewer - Read only
            rolePermissions.AddRange(new[]
            {
                new RolePermission { RoleId = 4, PermissionId = 1 }, // RouteView
                new RolePermission { RoleId = 4, PermissionId = 7 }  // ProductView
            });

            builder.Entity<RolePermission>().HasData(rolePermissions);
        }
    }
}
