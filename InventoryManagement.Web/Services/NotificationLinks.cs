using System.Security.Claims;
using InventoryManagement.Web.Extensions;
using InventoryManagement.Web.Models.DTOs;
using Microsoft.Extensions.Localization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace InventoryManagement.Web.Services
{
    /// <summary>Where a notification leads for the user reading it: the record it is about, when they may open it.
    /// The Notifications page and the bell both use it, so a notification opens the same place in either.</summary>
    public static class NotificationLinks
    {
        public static (string Url, string Label)? For(NotificationDto notification, ClaimsPrincipal user, IStringLocalizer localizer)
        {
            if (string.IsNullOrWhiteSpace(notification.Data)) return null;
            try
            {
                var data = JObject.Parse(notification.Data);
                int? Id(string name) => data.GetValue(name, StringComparison.OrdinalIgnoreCase)
                    is JValue { Type: JTokenType.Integer } value ? (int)value : null;

                if (Id("approvalRequestId") is int requestId)
                    return (((user.HasPermission("approval.view") || user.HasPermission("approval.decide")) && notification.Type == "ApprovalRequest" ? "/Approvals" : "/MyRequests") + "?open=" + requestId,
                            localizer["View request #{0}", requestId].Value);
                if (Id("routeId") is int routeId && user.HasPermission("route.view"))
                    return ($"/Routes/Details/{routeId}", localizer["View route"].Value);
                // A deleted product links to the Deleted products page, or to the audit log without that permission
                if (notification.Title == "Product Deleted" || notification.Title == localizer["Product Deleted"].Value)   // Stored in English, shown translated
                {
                    if (Id("productId") is not int deletedId) return null;
                    if (user.HasPermission("product.deleted.view"))
                        return ($"/Products/Deleted/{deletedId}", localizer["View deleted product"].Value);
                    return user.HasPermission("audit.view")
                        ? ($"/Audit?entityType=Product&entityId={deletedId}", localizer["View in audit log"].Value)
                        : null;
                }
                if (Id("productId") is int productId && user.HasPermission("product.view"))
                    return ($"/Products/Details/{productId}", localizer["View product"].Value);
            }
            catch (JsonException) { }
            return null;
        }
    }
}
