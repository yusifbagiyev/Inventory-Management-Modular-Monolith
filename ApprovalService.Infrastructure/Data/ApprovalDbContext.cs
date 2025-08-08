using ApprovalService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApprovalService.Infrastructure.Data
{
    public class ApprovalDbContext : DbContext
    {
        public ApprovalDbContext(DbContextOptions<ApprovalDbContext> options) : base(options) { }

        public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
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

                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.RequestedById);
                entity.HasIndex(e => e.CreatedAt);
            });
        }
    }
}