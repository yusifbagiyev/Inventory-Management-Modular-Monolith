using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RouteService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RouteWhatsAppStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "WhatsAppAt",
                schema: "route",
                table: "InventoryRoutes",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppError",
                schema: "route",
                table: "InventoryRoutes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppStatus",
                schema: "route",
                table: "InventoryRoutes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WhatsAppAt",
                schema: "route",
                table: "InventoryRoutes");

            migrationBuilder.DropColumn(
                name: "WhatsAppError",
                schema: "route",
                table: "InventoryRoutes");

            migrationBuilder.DropColumn(
                name: "WhatsAppStatus",
                schema: "route",
                table: "InventoryRoutes");
        }
    }
}
