using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using RouteService.Domain.Entities;
using SharedServices.Persistence;

namespace RouteService.Infrastructure.Data
{
    public class RouteDbContext : DbContext
    {
        public const string Schema = "route";

        public RouteDbContext(DbContextOptions<RouteDbContext> options) : base(options) { }

        public DbSet<InventoryRoute> InventoryRoutes => Set<InventoryRoute>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(Schema);

            modelBuilder.Entity<InventoryRoute>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.OwnsOne(e => e.ProductSnapshot, snapshot =>
                {
                    snapshot.Property(s => s.ProductId).HasColumnName("ProductId");
                    snapshot.Property(s => s.InventoryCode).HasColumnName("InventoryCode");
                    snapshot.Property(s => s.Model).HasColumnName("Model").HasMaxLength(100);
                    snapshot.Property(s => s.Vendor).HasColumnName("Vendor").HasMaxLength(100);
                    snapshot.Property(s => s.CategoryName).HasColumnName("CategoryName").HasMaxLength(100);
                    snapshot.Property(s => s.IsWorking).HasColumnName("IsWorking");

                    snapshot.HasIndex(s => s.ProductId).HasDatabaseName("IX_InventoryRoutes_ProductId");
                });

                entity.Property(e => e.RouteType).HasConversion<string>();
                entity.Property(e => e.FromDepartmentName).HasMaxLength(100);
                entity.Property(e => e.ToDepartmentName).HasMaxLength(100);
                entity.Property(e => e.FromWorker).HasMaxLength(100);
                entity.Property(e => e.ToWorker).HasMaxLength(100);
                entity.Property(e => e.Notes).HasMaxLength(500);
                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamp without time zone");
                entity.Property(e => e.CompletedAt)
                      .HasColumnType("timestamp without time zone");

                // PostgreSQL's xmin system column as an optimistic concurrency token: two users
                // completing the same route at once can no longer both succeed.
                entity.Property<uint>("xmin").IsRowVersion();

                entity.HasIndex(e => e.FromDepartmentId).HasDatabaseName("IX_InventoryRoutes_FromDepartmentId");
                entity.HasIndex(e => e.ToDepartmentId).HasDatabaseName("IX_InventoryRoutes_ToDepartmentId");
                entity.HasIndex(e => e.CreatedAt);
                // Default list order: pending first, then most recently completed.
                entity.HasIndex(e => new { e.IsCompleted, e.CompletedAt });
            });
        }
    }

    /// <summary>Used by `dotnet ef` only.</summary>
    public class RouteDbContextFactory : IDesignTimeDbContextFactory<RouteDbContext>
    {
        public RouteDbContext CreateDbContext(string[] args)
            => new(ModuleDbContextExtensions.DesignTimeOptions<RouteDbContext>(RouteDbContext.Schema));
    }
}
