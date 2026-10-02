namespace SharedServices.Identity
{
    // Rows are seeded in IdentityDbContext. A new constant needs a seed row and a migration there.
    /// <summary>Every permission. Admins hold them all without a grant.</summary>
    public static class AllPermissions
    {
        // Dashboard
        public const string DashboardView = "dashboard.view";

        // Routes
        public const string RouteView = "route.view";
        public const string RouteCreate = "route.create";
        public const string RouteCreateDirect = "route.create.direct";
        public const string RouteUpdate = "route.update";
        public const string RouteUpdateDirect = "route.update.direct";
        public const string RouteDelete = "route.delete";
        public const string RouteDeleteDirect = "route.delete.direct";
        public const string RouteComplete = "route.complete";
        public const string RouteExport = "route.export";

        // Products
        public const string ProductView = "product.view";
        public const string ProductCreate = "product.create";
        public const string ProductCreateDirect = "product.create.direct";
        public const string ProductUpdate = "product.update";
        public const string ProductUpdateDirect = "product.update.direct";
        public const string ProductDelete = "product.delete";
        public const string ProductDeleteDirect = "product.delete.direct";
        /// <summary>Changes a product's inventory code right away. There is no approval step.</summary>
        public const string ProductCodeUpdate = "product.code.update";
        public const string ProductExport = "product.export";
        /// <summary>Opens the Deleted products page.</summary>
        public const string ProductDeletedView = "product.deleted.view";

        // Categories
        public const string CategoryView = "category.view";
        public const string CategoryCreate = "category.create";
        public const string CategoryUpdate = "category.update";
        public const string CategoryDelete = "category.delete";

        // Departments
        public const string DepartmentView = "department.view";
        public const string DepartmentCreate = "department.create";
        public const string DepartmentUpdate = "department.update";
        public const string DepartmentDelete = "department.delete";
        /// <summary>Exports a department's inventory as a Word document.</summary>
        public const string DepartmentExport = "department.export";

        // Approvals
        public const string ApprovalView = "approval.view";
        /// <summary>Approves or rejects requests. Holders are notified of new ones.</summary>
        public const string ApprovalDecide = "approval.decide";

        // Users. Only Admins change roles and permissions.
        public const string UserView = "user.view";
        /// <summary>Manages non-admin users, including passwords and deactivation.</summary>
        public const string UserManage = "user.manage";

        // Audit log
        public const string AuditView = "audit.view";
    }
}
