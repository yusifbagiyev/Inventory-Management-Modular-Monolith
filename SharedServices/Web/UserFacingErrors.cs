namespace SharedServices.Web
{
    /// <summary>
    /// Whether an exception's message may be shown to the caller. The modules throw
    /// InvalidOperationException / ArgumentException with messages meant for users ("route is
    /// already completed"); the framework, EF Core and Npgsql throw the same types with internal
    /// text (queries, connection details), which stays in the log.
    /// </summary>
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
