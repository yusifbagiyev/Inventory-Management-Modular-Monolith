using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditService.Migrations
{
    /// <inheritdoc />
    public partial class AuditProductDeletions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A deleted product is kept, so its deletion was filed as an update and the Deleted filter missed it
            migrationBuilder.Sql("""
                UPDATE audit."AuditEntries"
                SET "Operation" = 'Deleted'
                WHERE "Operation" = 'Updated'
                  AND CASE WHEN "Changes" LIKE '%"IsDeleted"%' AND "Changes" !~ '\\u0000'
                           THEN "Changes"::jsonb @> '[{"field": "IsDeleted", "new": "true"}]'
                           ELSE false END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE audit."AuditEntries"
                SET "Operation" = 'Updated'
                WHERE "Operation" = 'Deleted'
                  AND CASE WHEN "Changes" LIKE '%"IsDeleted"%' AND "Changes" !~ '\\u0000'
                           THEN "Changes"::jsonb @> '[{"field": "IsDeleted", "new": "true"}]'
                           ELSE false END;
                """);
        }
    }
}
