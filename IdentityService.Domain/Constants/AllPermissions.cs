namespace IdentityService.Domain.Constants
{
    public static class AllPermissions
    {
        // Route Permissions
        public const string RouteView = "route.view";
        public const string RouteCreate = "route.create";
        public const string RouteUpdate = "route.update";
        public const string RouteDelete = "route.delete";
        public const string RouteComplete = "route.complete";
        public const string RouteBatchDelete = "route.batch_delete";

        // Product Permissions
        public const string ProductView = "product.view";
        public const string ProductCreate = "product.create";
        public const string ProductUpdate = "product.update";
        public const string ProductDelete = "product.delete";
        public const string ProductTransfer = "product.transfer";

        // Admin Permissions
        public const string UserManage = "user.manage";
        public const string RoleManage = "role.manage";
        public const string SystemConfig = "system.config";
    }
}