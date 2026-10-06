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

        /// <summary>PostgreSQL's regexp_replace with its flags, as the translated Regex.Replace changes only the first match.</summary>
        public static string RegexpReplace(string input, string pattern, string replacement, string flags)
            => throw new NotSupportedException("Only translated inside a query");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(Schema);

            modelBuilder.HasDbFunction(() => RegexpReplace(default!, default!, default!, default!))
                .HasName("regexp_replace")
                .IsBuiltIn();

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

                // The xmin column is the concurrency token, so two approvers cannot both run the action
                entity.Property<uint>("xmin").IsRowVersion();

                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.RequestedById);
                entity.HasIndex(e => e.CreatedAt);
            });
        }
    }

    /// <summary>Used only by dotnet ef.</summary>
    public class ApprovalDbContextFactory : IDesignTimeDbContextFactory<ApprovalDbContext>
    {
        public ApprovalDbContext CreateDbContext(string[] args)
            => new(ModuleDbContextExtensions.DesignTimeOptions<ApprovalDbContext>(ApprovalDbContext.Schema));
    }
}
