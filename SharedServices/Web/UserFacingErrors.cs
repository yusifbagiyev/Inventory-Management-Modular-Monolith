namespace SharedServices.Web
{
    /// <summary>Shows only our own exception messages, since framework, EF Core, Npgsql and library ones carry internal details.</summary>
    public static class UserFacingErrors
    {
        public const string Generic = "An error occurred while processing your request";

        // The app's own assemblies, so a library such as MediatR, SkiaSharp or Newtonsoft.Json never counts as ours
        private static readonly string[] OwnAssemblies =
        [
            "SharedServices", "InventoryManagement.", "IdentityService.", "ProductService.", "RouteService.",
            "ApprovalService.", "NotificationService.", "AuditService"
        ];

        public static bool IsUserFacing(Exception exception)
        {
            var assembly = exception.TargetSite?.Module.Assembly.GetName().Name;
            return assembly != null && OwnAssemblies.Any(own => own.EndsWith('.')
                ? assembly.StartsWith(own, StringComparison.Ordinal)
                : assembly == own);
        }

        /// <summary>The exception's message when it is one of ours, else the generic text.</summary>
        public static string MessageOf(Exception exception) => IsUserFacing(exception) ? exception.Message : Generic;
    }
}
