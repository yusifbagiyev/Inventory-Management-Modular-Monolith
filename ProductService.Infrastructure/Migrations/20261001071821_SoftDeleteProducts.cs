using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SoftDeleteProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_InventoryCode",
                schema: "product",
                table: "Products");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "product",
                table: "Products",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "product",
                table: "Products",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "product",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Products_InventoryCode",
                schema: "product",
                table: "Products",
                column: "InventoryCode",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_InventoryCode",
                schema: "product",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "product",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "product",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "product",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_Products_InventoryCode",
                schema: "product",
                table: "Products",
                column: "InventoryCode",
                unique: true);
        }
    }
}
