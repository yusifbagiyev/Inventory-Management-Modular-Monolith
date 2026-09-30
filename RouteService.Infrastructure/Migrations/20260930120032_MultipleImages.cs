using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RouteService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MultipleImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "ImageUrls",
                schema: "route",
                table: "InventoryRoutes",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            // The existing single image becomes the first (cover) image of the list.
            migrationBuilder.Sql(
                """UPDATE route."InventoryRoutes" SET "ImageUrls" = ARRAY["ImageUrl"] WHERE "ImageUrl" IS NOT NULL AND "ImageUrl" <> '';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrls",
                schema: "route",
                table: "InventoryRoutes");
        }
    }
}
