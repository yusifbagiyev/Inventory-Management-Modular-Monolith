namespace SharedServices.Web
{
    // The framework, EF Core and Npgsql throw the same exception types as our modules, but with internal details.
    /// <summary>Decides whether an exception's message may be shown to the caller.</summary>
    public static class UserFacingErrors
    {
        public const string Generic = "An error occurred while processing your request";

        public static bool IsUserFacing(Exception exception)
        {
            var assembly = exception.TargetSite?.Module.Assembly.GetName().Name;
            return assembly != null
                && !assembly.StartsWith("System", StringComparison.Ordinal)
                && !assembly.StartsWith("Microsoft", StringComparison.Ordinal)
                && !assembly.StartsWith("Npgsql", StringComparison.Ordinal)
                && assembly != "netstandard";
        }

        /// <summary>The exception's message when it is one of ours, else the generic text.</summary>
        public static string MessageOf(Exception exception) => IsUserFacing(exception) ? exception.Message : Generic;
    }
}
