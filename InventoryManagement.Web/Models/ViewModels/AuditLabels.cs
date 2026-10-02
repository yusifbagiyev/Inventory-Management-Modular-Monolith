using System.Text.RegularExpressions;

namespace InventoryManagement.Web.Models.ViewModels
{
    /// <summary>Readable English keys for stored audit actions and approval request types.</summary>
    public static class AuditLabels
    {
        private static readonly Dictionary<string, string> Actions = new(StringComparer.OrdinalIgnoreCase)
        {
            // The first command of the request
            ["CreateProduct"] = "Added a product",
            ["UpdateProduct"] = "Edited a product",
            ["DeleteProduct"] = "Deleted a product",
            ["UpdateProductInventoryCode"] = "Changed an inventory code",
            ["TransferInventory"] = "Created a transfer",
            ["CompleteRoute"] = "Completed a transfer",
            ["UpdateRoute"] = "Edited a transfer",
            ["DeleteRoute"] = "Deleted a route",
            ["CreateCategory"] = "Added a category",
            ["UpdateCategory"] = "Edited a category",
            ["DeleteCategory"] = "Deleted a category",
            ["CreateDepartment"] = "Added a department",
            ["UpdateDepartment"] = "Edited a department",
            ["DeleteDepartment"] = "Deleted a department",
            ["CreateApprovalRequest"] = "Sent a request for approval",
            ["ApproveRequest"] = "Approved a request",
            ["RejectRequest"] = "Rejected a request",
            ["CancelRequest"] = "Cancelled a request",

            // Endpoints without a command, mostly identity and sign-in
            ["Account.Login"] = "Sign-in",
            ["Account.Logout"] = "Sign-out",
            ["Account.ChangePassword"] = "Changed own password",
            ["Auth.Login"] = "Sign-in",
            ["Auth.Logout"] = "Sign-out",
            ["Auth.ChangePassword"] = "Changed own password",
            ["UserManagement.Create"] = "Created a user",
            ["Auth.RegisterByAdmin"] = "Created a user",
            ["UserManagement.Edit"] = "Edited a user",
            ["Auth.UpdateUser"] = "Edited a user",
            ["UserManagement.Delete"] = "Deleted a user",
            ["Auth.DeleteUser"] = "Deleted a user",
            ["UserManagement.ToggleStatus"] = "Activated or deactivated a user",
            ["UserManagement.QuickToggleStatus"] = "Activated or deactivated a user",
            ["Auth.ToggleUserStatus"] = "Activated or deactivated a user",
            ["UserManagement.ResetPassword"] = "Reset a user's password",
            ["Auth.ResetPassword"] = "Reset a user's password",
            ["UserManagement.TogglePermission"] = "Changed a user's permissions",
            ["UserManagement.ToggleRolePermission"] = "Changed a role's permissions",
            ["Auth.GrantPermission"] = "Changed a user's permissions",
            ["Auth.RevokePermission"] = "Changed a user's permissions",
            ["Auth.AssignRole"] = "Changed a user's role",
            ["Auth.RemoveRole"] = "Changed a user's role",
        };

        private static readonly Dictionary<string, string> RequestTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["product.create"] = "New product",
            ["product.update"] = "Product update",
            ["product.delete"] = "Product deletion",
            ["product.transfer"] = "Transfer",
            ["route.update"] = "Route update",
            ["route.delete"] = "Route deletion",
        };

        /// <summary>Known actions get a sentence, unknown ones are split into words.</summary>
        public static string Action(string action)
        {
            if (Actions.TryGetValue(action, out var text)) return text;
            var words = Regex.Replace(action.Replace(".", ": "), "(?<=[a-z])(?=[A-Z])", " ");
            return words.Length > 1 ? char.ToUpperInvariant(words[0]) + words[1..].ToLowerInvariant() : words;
        }

        /// <summary>Unknown request types come back unchanged.</summary>
        public static string RequestType(string? value)
            => value != null && RequestTypes.TryGetValue(value, out var text) ? text : value ?? "";

        public static bool IsRequestType(string? value) => value != null && RequestTypes.ContainsKey(value);
    }
}
