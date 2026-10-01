using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace IdentityService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UserPermissionsOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Users keep what they can do today: every non-Admin's role permissions become their own
            // permissions; Operators become Users; then the Operator and User roles lose their grants
            // (whatever the database holds - the old Operator seed differed between installations).
            migrationBuilder.Sql(@"
INSERT INTO identity.""UserPermissions"" (""UserId"", ""PermissionId"", ""GrantedAt"", ""GrantedBy"")
SELECT DISTINCT ur.""UserId"", rp.""PermissionId"", now()::timestamp, 'migration: role permissions'
FROM identity.""AspNetUserRoles"" ur
JOIN identity.""RolePermissions"" rp ON rp.""RoleId"" = ur.""RoleId""
WHERE ur.""RoleId"" IN (2, 3)
  AND NOT EXISTS (SELECT 1 FROM identity.""AspNetUserRoles"" a WHERE a.""UserId"" = ur.""UserId"" AND a.""RoleId"" = 1)
ON CONFLICT DO NOTHING;

INSERT INTO identity.""AspNetUserRoles"" (""UserId"", ""RoleId"")
SELECT ""UserId"", 3 FROM identity.""AspNetUserRoles"" WHERE ""RoleId"" = 2
ON CONFLICT DO NOTHING;

DELETE FROM identity.""AspNetUserRoles"" WHERE ""RoleId"" = 2;
DELETE FROM identity.""RolePermissions"" WHERE ""RoleId"" IN (2, 3);
");

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 1, 2 });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 2, 2 });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 4, 2 });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 6, 2 });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 8, 2 });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 9, 2 });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 10, 2 });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 12, 2 });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 14, 2 });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 1, 3 });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 9, 3 });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.InsertData(
                schema: "identity",
                table: "Permissions",
                columns: new[] { "Id", "Category", "Description", "Name" },
                values: new object[,]
                {
                    { 16, "Product", "Change inventory codes", "product.code.update" },
                    { 17, "Product", "Export products to PDF", "product.export" },
                    { 18, "Route", "Export routes and timelines to PDF", "route.export" },
                    { 19, "Dashboard", "View the dashboard", "dashboard.view" },
                    { 20, "Category", "View categories", "category.view" },
                    { 21, "Category", "Create categories", "category.create" },
                    { 22, "Category", "Edit categories", "category.update" },
                    { 23, "Category", "Delete categories", "category.delete" },
                    { 24, "Department", "View departments", "department.view" },
                    { 25, "Department", "Create departments", "department.create" },
                    { 26, "Department", "Edit departments", "department.update" },
                    { 27, "Department", "Delete departments", "department.delete" },
                    { 28, "Department", "Export a department's inventory to Word", "department.export" },
                    { 29, "Approval", "View approval requests", "approval.view" },
                    { 30, "Approval", "Approve or reject requests", "approval.decide" },
                    { 31, "User", "View users", "user.view" },
                    { 32, "User", "Create, edit, deactivate and delete users (not administrators)", "user.manage" },
                    { 33, "Audit", "View the audit log", "audit.view" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.InsertData(
                schema: "identity",
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Description", "Name", "NormalizedName" },
                values: new object[] { 2, "OPERATOR_STAMP_123", "", "Operator", "OPERATOR" });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 1, 3 },
                    { 9, 3 },
                    { 1, 2 },
                    { 2, 2 },
                    { 4, 2 },
                    { 6, 2 },
                    { 8, 2 },
                    { 9, 2 },
                    { 10, 2 },
                    { 12, 2 },
                    { 14, 2 }
                });
        }
    }
}
