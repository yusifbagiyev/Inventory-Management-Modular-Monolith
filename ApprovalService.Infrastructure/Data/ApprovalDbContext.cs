using ApprovalService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SharedServices.Persistence;

namespace ApprovalService.Infrastructure.Data
{
    public class ApprovalDbContext : DbContext
    {
        public const string Schema = "approval";

        public ApprovalDbContext(DbContextOptions<ApprovalDbContext> options) : base(options) { }

        public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(Schema);

            modelBuilder.Entity<ApprovalRequest>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.RequestType).HasMaxLength(100).IsRequired();
                entity.Property(e => e.EntityType).HasMaxLength(100).IsRequired();
                entity.Property(e => e.ActionData).IsRequired();
                entity.Property(e => e.RequestedByName).HasMaxLength(200).IsRequired();
                entity.Property(e => e.ApprovedByName).HasMaxLength(200);
                entity.Property(e => e.RejectionReason).HasMaxLength(500);
                entity.Property(e => e.Status).HasConversion<string>();
                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamp without time zone");
                entity.Property(e => e.ProcessedAt)
                      .HasColumnType("timestamp without time zone");
                entity.Property(e => e.ExecutedAt)
                      .HasColumnType("timestamp without time zone");

                // xmin as optimistic concurrency token: two admins approving the same request at
                // once can no longer both execute its action.
                entity.Property<uint>("xmin").IsRowVersion();

                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.RequestedById);
                entity.HasIndex(e => e.CreatedAt);
            });
        }
    }

    /// <summary>Used by `dotnet ef` only.</summary>
    public class ApprovalDbContextFactory : IDesignTimeDbContextFactory<ApprovalDbContext>
    {
        public ApprovalDbContext CreateDbContext(string[] args)
            => new(ModuleDbContextExtensions.DesignTimeOptions<ApprovalDbContext>(ApprovalDbContext.Schema));
    }
}
