using SharedServices.Identity;

namespace InventoryManagement.Web.Models.ViewModels
{
    /// <summary>One action in the permission editor. With Direct set it is a two-level choice of none, with approval or direct.</summary>
    public sealed record PermissionItem(string Permission, string Label, string? Direct = null, bool IsView = false)
    {
        public IEnumerable<string> Names => Direct == null ? [Permission] : [Permission, Direct];
    }

    public sealed record PermissionArea(string Key, string Title, string Icon, IReadOnlyList<PermissionItem> Items);

    /// <summary>Every permission grouped by page in plain words. Anything missing here still shows under Other.</summary>
    public static class PermissionCatalog
    {
        public static IReadOnlyList<PermissionArea> Areas { get; } =
        [
            new("dashboard", "Dashboard", "fa-table-cells-large",
            [
                new(AllPermissions.DashboardView, "View the dashboard", IsView: true),
            ]),
            new("products", "Products", "fa-box",
            [
                new(AllPermissions.ProductView, "View products", IsView: true),
                new(AllPermissions.ProductCreate, "Add products", AllPermissions.ProductCreateDirect),
                new(AllPermissions.ProductUpdate, "Edit products", AllPermissions.ProductUpdateDirect),
                new(AllPermissions.ProductDelete, "Delete products", AllPermissions.ProductDeleteDirect),
                new(AllPermissions.ProductCodeUpdate, "Change inventory codes"),
                new(AllPermissions.ProductExport, "Export products to PDF"),
                new(AllPermissions.ProductDeletedView, "View deleted products"),
            ]),
            new("routes", "Routes", "fa-route",
            [
                new(AllPermissions.RouteView, "View routes and history", IsView: true),
                new(AllPermissions.RouteCreate, "Create transfers", AllPermissions.RouteCreateDirect),
                new(AllPermissions.RouteUpdate, "Edit transfers", AllPermissions.RouteUpdateDirect),
                new(AllPermissions.RouteDelete, "Delete routes", AllPermissions.RouteDeleteDirect),
                new(AllPermissions.RouteComplete, "Complete transfers"),
                new(AllPermissions.RouteExport, "Export routes and timelines to PDF"),
            ]),
            new("categories", "Categories", "fa-tags",
            [
                new(AllPermissions.CategoryView, "View categories", IsView: true),
                new(AllPermissions.CategoryCreate, "Create categories"),
                new(AllPermissions.CategoryUpdate, "Edit categories"),
                new(AllPermissions.CategoryDelete, "Delete categories"),
            ]),
            new("departments", "Departments", "fa-sitemap",
            [
                new(AllPermissions.DepartmentView, "View departments", IsView: true),
                new(AllPermissions.DepartmentCreate, "Create departments"),
                new(AllPermissions.DepartmentUpdate, "Edit departments"),
                new(AllPermissions.DepartmentDelete, "Delete departments"),
                new(AllPermissions.DepartmentExport, "Export a department's inventory to Word"),
            ]),
            new("approvals", "Approvals", "fa-clipboard-check",
            [
                new(AllPermissions.ApprovalView, "View approval requests", IsView: true),
                new(AllPermissions.ApprovalDecide, "Approve or reject requests"),
            ]),
            new("users", "Users", "fa-users",
            [
                new(AllPermissions.UserView, "View users", IsView: true),
                new(AllPermissions.UserManage, "Create, edit, deactivate and delete users (not administrators)"),
            ]),
            new("audit", "Audit log", "fa-clipboard-list",
            [
                new(AllPermissions.AuditView, "View the audit log", IsView: true),
            ]),
        ];

        public static IReadOnlySet<string> Known { get; } =
            Areas.SelectMany(a => a.Items).SelectMany(i => i.Names).ToHashSet(StringComparer.OrdinalIgnoreCase);

        /// <summary>Two-level items return none, approval or direct. Others return on or none.</summary>
        public static string State(PermissionItem item, ISet<string> held)
        {
            if (item.Direct != null && held.Contains(item.Direct)) return "direct";
            if (held.Contains(item.Permission)) return item.Direct != null ? "approval" : "on";
            return "none";
        }
    }
}
