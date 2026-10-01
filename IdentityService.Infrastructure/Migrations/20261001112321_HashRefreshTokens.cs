using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HashRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Refresh tokens are now stored as their SHA-256 (hex, as TokenService.Hash): existing
            // ones are hashed in place, so signed-in API clients keep working. Down cannot undo it.
            migrationBuilder.Sql(@"
                UPDATE identity.""RefreshTokens""
                SET ""Token"" = encode(sha256(convert_to(""Token"", 'UTF8')), 'hex'),
                    ""ReplacedByToken"" = CASE WHEN ""ReplacedByToken"" IS NULL THEN NULL
                                             ELSE encode(sha256(convert_to(""ReplacedByToken"", 'UTF8')), 'hex') END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
