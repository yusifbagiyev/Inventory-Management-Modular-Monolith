namespace SharedServices.Identity
{
    /// <summary>
    /// Every permission. Admins hold all of them implicitly; everyone else holds exactly the ones an
    /// admin granted them (the User role grants nothing by itself). The rows are seeded in
    /// IdentityDbContext - a new constant needs a seed row (and a migration) there.
    /// </summary>
    public static class AllPermissions
    {
        // Dashboard
        public const string DashboardView = "dashboard.view";

        // Route Permissions
        public const string RouteView = "route.view";
        public const string RouteCreate = "route.create";
        public const string RouteCreateDirect = "route.create.direct";
        public const string RouteUpdate = "route.update";
        public const string RouteUpdateDirect = "route.update.direct";
        public const string RouteDelete = "route.delete";
        public const string RouteDeleteDirect = "route.delete.direct";
        public const string RouteComplete = "route.complete";
        public const string RouteExport = "route.export";

        // Product Permissions
        public const string ProductView = "product.view";
        public const string ProductCreate = "product.create";
        public const string ProductCreateDirect = "product.create.direct";
        public const string ProductUpdate = "product.update";
        public const string ProductUpdateDirect = "product.update.direct";
        public const string ProductDelete = "product.delete";
        public const string ProductDeleteDirect = "product.delete.direct";
        /// <summary>Change a product's inventory code (immediately; there is no approval step).</summary>
        public const string ProductCodeUpdate = "product.code.update";
        public const string ProductExport = "product.export";

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
        /// <summary>Department inventory as a Word document.</summary>
        public const string DepartmentExport = "department.export";

        // Approvals
        public const string ApprovalView = "approval.view";
        /// <summary>Approve or reject requests; holders are notified of new requests.</summary>
        public const string ApprovalDecide = "approval.decide";

        // Users. Roles and permissions themselves are changed by Admins only.
        public const string UserView = "user.view";
        /// <summary>Create, edit, (de)activate, reset passwords and delete non-admin users.</summary>
        public const string UserManage = "user.manage";

        // Audit log
        public const string AuditView = "audit.view";
    }
}
