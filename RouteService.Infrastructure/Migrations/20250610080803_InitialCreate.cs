using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RouteService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryRoutes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RouteType = table.Column<string>(type: "text", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    InventoryCode = table.Column<int>(type: "integer", nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Vendor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CategoryName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsWorking = table.Column<bool>(type: "boolean", nullable: false),
                    FromDepartmentId = table.Column<int>(type: "integer", nullable: true),
                    FromDepartmentName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ToDepartmentId = table.Column<int>(type: "integer", nullable: false),
                    ToDepartmentName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FromWorker = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ToWorker = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryRoutes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRoutes_CreatedAt",
                table: "InventoryRoutes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRoutes_FromDepartmentId",
                table: "InventoryRoutes",
                column: "FromDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRoutes_ProductId",
                table: "InventoryRoutes",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRoutes_ToDepartmentId",
                table: "InventoryRoutes",
                column: "ToDepartmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryRoutes");
        }
    }
}
