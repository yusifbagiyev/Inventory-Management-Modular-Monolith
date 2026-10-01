using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Design;
using ProductService.Domain.Entities;
using SharedServices.Persistence;

namespace ProductService.Infrastructure.Data
{
    public class ProductDbContext : DbContext
    {
        public const string Schema = "product";

        public ProductDbContext(DbContextOptions<ProductDbContext> options) : base(options) { }

        public DbSet<Product> Products => Set<Product>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Department> Departments => Set<Department>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(Schema);

            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.Id);
                // Deleted products are kept but out of sight everywhere (IgnoreQueryFilters to see
                // them), and their inventory code can be given to a new product.
                entity.HasQueryFilter(e => !e.IsDeleted);
                entity.HasIndex(e => e.InventoryCode).IsUnique().HasFilter("\"IsDeleted\" = false");
                entity.Property(e => e.DeletedBy).HasMaxLength(200);
                entity.Property(e => e.DeletedAt).HasColumnType("timestamp without time zone");
                entity.HasIndex(e => e.CreatedAt);
                entity.Property(e => e.Model).HasMaxLength(50);
                entity.Property(e => e.Vendor).HasMaxLength(30);
                entity.Property(e => e.Color).HasMaxLength(30);
                // Specifications live with the product as a jsonb array of {Name, Value}.
                entity.Property(e => e.Specifications)
                      .HasColumnType("jsonb")
                      .HasDefaultValueSql("'[]'::jsonb")
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                          v => JsonSerializer.Deserialize<List<ProductSpecification>>(v, (JsonSerializerOptions?)null) ?? new List<ProductSpecification>(),
                          new ValueComparer<List<ProductSpecification>>(
                              (a, b) => a!.SequenceEqual(b!),
                              c => c.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
                              c => c.ToList()));
                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamp without time zone");
                entity.Property(e => e.UpdatedAt)
                      .HasColumnType("timestamp without time zone");

                // Restrict, not the EF default of Cascade: deleting a category or department
                // must never silently delete the products assigned to it. The delete commands
                // check for referencing products first and return a 409 with the count.
                entity.HasOne(e => e.Category)
                    .WithMany(c => c.Products)
                    .HasForeignKey(e => e.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Department)
                    .WithMany(d => d.Products)
                    .HasForeignKey(e => e.DepartmentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(e => e.Id);
                // Matches the 100-character limit the create/update validators enforce; the column
                // used to be 20, so longer names passed validation and then failed with a 500.
                entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamp without time zone");
                entity.Property(e => e.UpdatedAt)
                      .HasColumnType("timestamp without time zone");
            });

            modelBuilder.Entity<Department>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamp without time zone");
                entity.Property(e => e.UpdatedAt)
                      .HasColumnType("timestamp without time zone");
            });
        }
    }

    /// <summary>Used by `dotnet ef` only.</summary>
    public class ProductDbContextFactory : IDesignTimeDbContextFactory<ProductDbContext>
    {
        public ProductDbContext CreateDbContext(string[] args)
            => new(ModuleDbContextExtensions.DesignTimeOptions<ProductDbContext>(ProductDbContext.Schema));
    }
}
