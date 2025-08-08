using Microsoft.EntityFrameworkCore;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Data
{
    public class NotificationDbContext : DbContext
    {
        public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }

        public DbSet<Notification> Notifications => Set<Notification>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Type).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Message).IsRequired();
                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamp without time zone");
                entity.Property(e => e.ReadAt)
                      .HasColumnType("timestamp without time zone");

                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => new { e.UserId, e.IsRead });
                entity.HasIndex(e => e.CreatedAt);
            });
        }
    }
}