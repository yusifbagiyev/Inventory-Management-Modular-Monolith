using IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SharedServices.Identity;
using SharedServices.Persistence;
using Microsoft.EntityFrameworkCore.Design;

namespace IdentityService.Infrastructure.Data
{
    public class IdentityDbContext : IdentityDbContext<User, Role, int>
    {
        public const string Schema = "identity";

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
            builder.HasDefaultSchema(Schema);

            builder.Entity<User>(entity =>
            {
                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamp without time zone");
                entity.Property(e => e.LastLoginAt)
                      .HasColumnType("timestamp without time zone");
            });

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
                entity.Property(u => u.GrantedAt)
                      .HasColumnType("timestamp without time zone");

                entity.HasKey(up => new { up.UserId, up.PermissionId });

                entity.HasOne(up => up.User)
                    .WithMany(u=>u.UserPermissions)
                    .HasForeignKey(up => up.UserId);

                entity.HasOne(up => up.Permission)
                    .WithMany(p=>p.UserPermissions)
                    .HasForeignKey(up => up.PermissionId);
            });

            builder.Entity<RefreshToken>(entity =>
            {
                entity.Property(u => u.CreatedAt)
                      .HasColumnType("timestamp without time zone");
                entity.Property(u => u.ExpiresAt)
                      .HasColumnType("timestamp without time zone");
                entity.Property(u => u.RevokedAt)
                      .HasColumnType("timestamp without time zone");

                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Token).IsUnique();

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Seed values must stay static, or every model build would differ and need a new migration
            SeedData(builder);
        }

        private void SeedData(ModelBuilder builder)
        {
            var user = new User
            {
                Id = 1,
                UserName = "admin",
                FirstName = "Demo",
                LastName = "Admin",
                NormalizedUserName = "ADMIN",
                Email = "admin@example.com",
                NormalizedEmail = "ADMIN@EXAMPLE.COM",
                SecurityStamp = "STATIC_SECURITY_STAMP_123",
                ConcurrencyStamp = "STATIC_CONCURRENCY_STAMP_123",
                PasswordHash = "AQAAAAIAAYagAAAAEE3gZfTp9NqZg1rPQY/+NJv3V+0ETlkIFMFVDjivbDBCcaYzJw1hoDsD7zkk78/KwA==",
                LockoutEnabled = false,
                EmailConfirmed = true,
                AccessFailedCount = 0,
                CreatedAt = new DateTime(2025, 8, 1, 0, 0, 0)
            };

            builder.Entity<User>().HasData(user);

            builder.Entity<IdentityUserRole<int>>().HasData(
                new IdentityUserRole<int> 
                { 
                    UserId = 1, RoleId = 1 ,
                }
            );

            var roles = new[]
            {
                new Role { Id = 1, Name = "Admin", NormalizedName = "ADMIN", ConcurrencyStamp = "ADMIN_STAMP_123" },
                new Role { Id = 3, Name = "User", NormalizedName = "USER", ConcurrencyStamp = "USER_STAMP_123" }
            };
            builder.Entity<Role>().HasData(roles);

            // A new permission needs a row here and a migration
            var permissions = new[]
            {
                // Route permissions
                new Permission { Id = 1, Name = AllPermissions.RouteView, Category = "Route", Description = "View routes" },
                new Permission { Id = 2, Name = AllPermissions.RouteCreate, Category = "Route", Description = "Create routes (requires approval)" },
                new Permission { Id = 3, Name = AllPermissions.RouteCreateDirect, Category = "Route", Description = "Create routes directly" },
                new Permission { Id = 4, Name = AllPermissions.RouteUpdate, Category = "Route", Description = "Update routes (requires approval)" },
                new Permission { Id = 5, Name = AllPermissions.RouteUpdateDirect, Category = "Route", Description = "Update routes directly" },
                new Permission { Id = 6, Name = AllPermissions.RouteDelete, Category = "Route", Description = "Delete routes (requires approval)" },
                new Permission { Id = 7, Name = AllPermissions.RouteDeleteDirect, Category = "Route", Description = "Delete routes directly" },
                new Permission { Id = 8, Name = AllPermissions.RouteComplete, Category = "Route", Description = "Complete routes" },
        
                // Product permissions
                new Permission { Id = 9, Name = AllPermissions.ProductView, Category = "Product", Description = "View products" },
                new Permission { Id = 10, Name = AllPermissions.ProductCreate, Category = "Product", Description = "Create products (requires approval)" },
                new Permission { Id = 11, Name = AllPermissions.ProductCreateDirect, Category = "Product", Description = "Create products directly" },
                new Permission { Id = 12, Name = AllPermissions.ProductUpdate, Category = "Product", Description = "Update products (requires approval)" },
                new Permission { Id = 13, Name = AllPermissions.ProductUpdateDirect, Category = "Product", Description = "Update products directly" },
                new Permission { Id = 14, Name = AllPermissions.ProductDelete, Category = "Product", Description = "Delete products (requires approval)" },
                new Permission { Id = 15, Name = AllPermissions.ProductDeleteDirect, Category = "Product", Description = "Delete products directly" },

                // Page and function permissions
                new Permission { Id = 16, Name = AllPermissions.ProductCodeUpdate, Category = "Product", Description = "Change inventory codes" },
                new Permission { Id = 17, Name = AllPermissions.ProductExport, Category = "Product", Description = "Export products to PDF" },
                new Permission { Id = 18, Name = AllPermissions.RouteExport, Category = "Route", Description = "Export routes and timelines to PDF" },
                new Permission { Id = 19, Name = AllPermissions.DashboardView, Category = "Dashboard", Description = "View the dashboard" },
                new Permission { Id = 20, Name = AllPermissions.CategoryView, Category = "Category", Description = "View categories" },
                new Permission { Id = 21, Name = AllPermissions.CategoryCreate, Category = "Category", Description = "Create categories" },
                new Permission { Id = 22, Name = AllPermissions.CategoryUpdate, Category = "Category", Description = "Edit categories" },
                new Permission { Id = 23, Name = AllPermissions.CategoryDelete, Category = "Category", Description = "Delete categories" },
                new Permission { Id = 24, Name = AllPermissions.DepartmentView, Category = "Department", Description = "View departments" },
                new Permission { Id = 25, Name = AllPermissions.DepartmentCreate, Category = "Department", Description = "Create departments" },
                new Permission { Id = 26, Name = AllPermissions.DepartmentUpdate, Category = "Department", Description = "Edit departments" },
                new Permission { Id = 27, Name = AllPermissions.DepartmentDelete, Category = "Department", Description = "Delete departments" },
                new Permission { Id = 28, Name = AllPermissions.DepartmentExport, Category = "Department", Description = "Export a department's inventory to Word" },
                new Permission { Id = 29, Name = AllPermissions.ApprovalView, Category = "Approval", Description = "View approval requests" },
                new Permission { Id = 30, Name = AllPermissions.ApprovalDecide, Category = "Approval", Description = "Approve or reject requests" },
                new Permission { Id = 31, Name = AllPermissions.UserView, Category = "User", Description = "View users" },
                new Permission { Id = 32, Name = AllPermissions.UserManage, Category = "User", Description = "Create, edit, deactivate and delete users (not administrators)" },
                new Permission { Id = 33, Name = AllPermissions.AuditView, Category = "Audit", Description = "View the audit log" },
                new Permission { Id = 34, Name = AllPermissions.ProductDeletedView, Category = "Product", Description = "View deleted products" },
            };
            builder.Entity<Permission>().HasData(permissions);

            var rolePermissions = new List<RolePermission>();

            // Admin passes every check anyway, so these rows only cover the route and product permissions
            for (int i = 1; i <= 15; i++)
            {
                rolePermissions.Add(new RolePermission { RoleId = 1, PermissionId = i });
            }

            // Only Admin gets seeded role permissions

            builder.Entity<RolePermission>().HasData(rolePermissions);

        }
    }

    /// <summary>Used only by dotnet ef.</summary>
    public class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
    {
        public IdentityDbContext CreateDbContext(string[] args)
            => new(ModuleDbContextExtensions.DesignTimeOptions<IdentityDbContext>(IdentityDbContext.Schema));
    }
}
