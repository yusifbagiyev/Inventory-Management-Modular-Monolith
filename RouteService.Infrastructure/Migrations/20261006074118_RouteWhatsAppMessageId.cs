using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RouteService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RouteWhatsAppMessageId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "WhatsAppMessageId",
                schema: "route",
                table: "InventoryRoutes",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WhatsAppMessageId",
                schema: "route",
                table: "InventoryRoutes");
        }
    }
}
