using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SharedServices.Persistence;

namespace AuditService.Data
{
    public class AuditDbContext : DbContext
    {
        public const string Schema = "audit";

        public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options) { }

        public DbSet<AuditEntry> Entries => Set<AuditEntry>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.HasDefaultSchema(Schema);
            builder.Entity<AuditEntry>(entity =>
            {
                entity.ToTable("AuditEntries");
                entity.HasKey(e => e.Id);
                // Local time like every other module.
                entity.Property(e => e.At).HasColumnType("timestamp without time zone");
                entity.Property(e => e.CorrelationId).HasMaxLength(100).IsRequired();
                entity.Property(e => e.UserName).HasMaxLength(200);
                entity.Property(e => e.IpAddress).HasMaxLength(64);
                entity.Property(e => e.Action).HasMaxLength(150).IsRequired();
                entity.Property(e => e.EntityType).HasMaxLength(100).IsRequired();
                entity.Property(e => e.EntityId).HasMaxLength(100);
                entity.Property(e => e.Label).HasMaxLength(600);
                entity.Property(e => e.Operation).HasMaxLength(30).IsRequired();
                // Plain text is enough. It is only shown and searched with ILIKE, never queried as JSON.
                entity.Property(e => e.Changes).HasColumnType("text").IsRequired();
                entity.HasIndex(e => e.At);
                entity.HasIndex(e => e.CorrelationId);
                entity.HasIndex(e => new { e.EntityType, e.EntityId });
                entity.HasIndex(e => e.UserId);
            });
        }
    }

    /// <summary>Used by dotnet ef only.</summary>
    public class AuditDbContextFactory : IDesignTimeDbContextFactory<AuditDbContext>
    {
        public AuditDbContext CreateDbContext(string[] args)
            => new(ModuleDbContextExtensions.DesignTimeOptions<AuditDbContext>(AuditDbContext.Schema));
    }
}
