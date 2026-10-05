using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditService.Migrations
{
    /// <inheritdoc />
    public partial class AuditChangesReadableText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Older rows hold letters as \u escapes that a text search cannot find, and jsonb writes them back as plain letters
            migrationBuilder.Sql("""
                UPDATE audit."AuditEntries"
                SET "Changes" = ("Changes"::jsonb)::text
                WHERE "Changes" ~ '\\u[0-9a-fA-F]{4}' AND "Changes" !~ '\\u0000';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only the spelling of the JSON changed, so there is nothing to undo
        }
    }
}
