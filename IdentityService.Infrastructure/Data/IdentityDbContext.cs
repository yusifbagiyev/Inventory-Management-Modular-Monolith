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
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }

        public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure RolePermission many-to-many
            builder.Entity<RolePermission>(entity =>
            {
                entity.HasKey(r => new { r.RoleId, r.PermissionId });

                entity.HasOne(r => r.Role)
                    .WithMany(r => r.RolePermissions)
                    .HasForeignKey(r => r.RoleId);

                entity.HasOne(r => r.Permission)
                    .WithMany(p => p.RolePermissions)
                    .HasForeignKey(rp => rp.PermissionId);
            });


            builder.Entity<UserPermission>(entity =>
            {
                entity.HasKey(up => new { up.UserId, up.PermissionId });

                entity.HasOne(up => up.User)
                    .WithMany()
                    .HasForeignKey(up => up.UserId);

                entity.HasOne(up => up.Permission)
                    .WithMany()
                    .HasForeignKey(up => up.PermissionId);
            });


            builder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Token).IsUnique();

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });


            // Seed initial data
            SeedData(builder);
        }

        private void SeedData(ModelBuilder builder)
        {
            var roles = new[]
            {
                new Role { Id = 1, Name = "Admin", NormalizedName = "ADMIN" },
                new Role { Id = 2, Name = "Manager", NormalizedName = "MANAGER" },
                new Role { Id = 3, Name = "User", NormalizedName = "USER" }
            };
            builder.Entity<Role>().HasData(roles);

            // Seed Permissions (expanded)
            var permissions = new[]
            {
                // Route permissions
                new Permission { Id = 1, Name = AllPermissions.RouteView, Category = "Route", Description = "View routes" },
                new Permission { Id = 2, Name = AllPermissions.RouteCreate, Category = "Route", Description = "Create routes" },
                new Permission { Id = 3, Name = AllPermissions.RouteUpdate, Category = "Route", Description = "Update routes (requires approval)" },
                new Permission { Id = 4, Name = AllPermissions.RouteUpdateDirect, Category = "Route", Description = "Update routes directly" },
                new Permission { Id = 5, Name = AllPermissions.RouteDelete, Category = "Route", Description = "Delete routes (requires approval)" },
                new Permission { Id = 6, Name = AllPermissions.RouteDeleteDirect, Category = "Route", Description = "Delete routes directly" },
        
                // Product permissions
                new Permission { Id = 7, Name = AllPermissions.ProductView, Category = "Product", Description = "View products" },
                new Permission { Id = 8, Name = AllPermissions.ProductCreate, Category = "Product", Description = "Create products (requires approval)" },
                new Permission { Id = 9, Name = AllPermissions.ProductCreateDirect, Category = "Product", Description = "Create products directly" },
                new Permission { Id = 10, Name = AllPermissions.ProductUpdate, Category = "Product", Description = "Update products (requires approval)" },
                new Permission { Id = 11, Name = AllPermissions.ProductUpdateDirect, Category = "Product", Description = "Update products directly" },
                new Permission { Id = 12, Name = AllPermissions.ProductDelete, Category = "Product", Description = "Delete products (requires approval)" },
                new Permission { Id = 13, Name = AllPermissions.ProductDeleteDirect, Category = "Product", Description = "Delete products directly" },
                new Permission { Id = 14, Name = AllPermissions.ProductTransfer, Category = "Product", Description = "Transfer products (requires approval)" },
                new Permission { Id = 15, Name = AllPermissions.ProductTransferDirect, Category = "Product", Description = "Transfer products directly" },
        
                // Admin permissions
                new Permission { Id = 16, Name = AllPermissions.UserManage, Category = "Admin", Description = "Manage users" },
                new Permission { Id = 17, Name = AllPermissions.RoleManage, Category = "Admin", Description = "Manage roles" },
                new Permission { Id = 18, Name = AllPermissions.ApprovalManage, Category = "Admin", Description = "Manage approvals" }
            };
            builder.Entity<Permission>().HasData(permissions);

            // Update Role Permissions
            var rolePermissions = new List<RolePermission>();

            // Admin - All direct permissions
            for (int i = 1; i <= 18; i++)
            {
                rolePermissions.Add(new RolePermission { RoleId = 1, PermissionId = i });
            }

            // Manager (Operator) - Request permissions only
            rolePermissions.AddRange(new[]
            {
                new RolePermission { RoleId = 2, PermissionId = 1 }, // RouteView
                new RolePermission { RoleId = 2, PermissionId = 2 }, // RouteCreate
                new RolePermission { RoleId = 2, PermissionId = 3 }, // RouteUpdate (request)
                new RolePermission { RoleId = 2, PermissionId = 5 }, // RouteDelete (request)
                new RolePermission { RoleId = 2, PermissionId = 7 }, // ProductView
                new RolePermission { RoleId = 2, PermissionId = 8 }, // ProductCreate (request)
                new RolePermission { RoleId = 2, PermissionId = 10 }, // ProductUpdate (request)
                new RolePermission { RoleId = 2, PermissionId = 12 }, // ProductDelete (request)
                new RolePermission { RoleId = 2, PermissionId = 14 } // ProductTransfer (request)
            });

            // User - View only
            rolePermissions.AddRange(new[]
            {
                new RolePermission { RoleId = 3, PermissionId = 1 }, // RouteView
                new RolePermission { RoleId = 3, PermissionId = 7 }  // ProductView
            });

            builder.Entity<RolePermission>().HasData(rolePermissions);
        }
    }
}