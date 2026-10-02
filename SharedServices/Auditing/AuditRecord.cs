namespace SharedServices.Auditing
{
    /// <summary>One field of an audited change. Values are display strings, null when empty.</summary>
    public sealed record AuditFieldChange(string Field, string? Old, string? New);

    /// <summary>One audited change or sign-in event. Records of one request share a CorrelationId.</summary>
    public sealed record AuditRecord(
        DateTime At,
        string CorrelationId,
        int? UserId,
        string? UserName,
        string? IpAddress,
        string Action,
        string EntityType,
        string? EntityId,
        string? Label,
        string Operation,
        IReadOnlyList<AuditFieldChange> Changes);

    /// <summary>Operations recorded in the audit log.</summary>
    public static class AuditOperations
    {
        public const string Created = "Created";
        public const string Updated = "Updated";
        public const string Deleted = "Deleted";
        public const string SignedIn = "SignedIn";
        public const string SignInFailed = "SignInFailed";
        public const string SignedOut = "SignedOut";
    }

    /// <summary>Stores audit records inside the request's transaction, so they commit with the change.</summary>
    public interface IAuditSink
    {
        Task WriteAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default);
        void Write(IReadOnlyList<AuditRecord> records);
    }
}
