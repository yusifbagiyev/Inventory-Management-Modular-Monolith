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
                entity.Property(e => e.WhatsAppStatus).HasMaxLength(20);
                entity.Property(e => e.WhatsAppError).HasMaxLength(500);
                entity.Property(e => e.WhatsAppAt).HasColumnType("timestamp without time zone");
                entity.Property(e => e.CompletedAt)
                      .HasColumnType("timestamp without time zone");

                // The xmin column is the concurrency token, so two users cannot both complete the same route.
                entity.Property<uint>("xmin").IsRowVersion();

                entity.HasIndex(e => e.FromDepartmentId).HasDatabaseName("IX_InventoryRoutes_FromDepartmentId");
                entity.HasIndex(e => e.ToDepartmentId).HasDatabaseName("IX_InventoryRoutes_ToDepartmentId");
                entity.HasIndex(e => e.CreatedAt);
                // Matches the default list order, pending first and then most recently completed.
                entity.HasIndex(e => new { e.IsCompleted, e.CompletedAt });
            });
        }
    }

    /// <summary>Used only by dotnet ef.</summary>
    public class RouteDbContextFactory : IDesignTimeDbContextFactory<RouteDbContext>
    {
        public RouteDbContext CreateDbContext(string[] args)
            => new(ModuleDbContextExtensions.DesignTimeOptions<RouteDbContext>(RouteDbContext.Schema));
    }
}
