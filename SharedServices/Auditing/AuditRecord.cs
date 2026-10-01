namespace SharedServices.Auditing
{
    /// <summary>One field of an audited change. Values are display strings (null: empty).</summary>
    public sealed record AuditFieldChange(string Field, string? Old, string? New);

    /// <summary>
    /// One audited change to one record (or an event such as a sign-in), with who did it and in
    /// which request. Records of one request share <see cref="CorrelationId"/>.
    /// </summary>
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

    /// <summary>
    /// Stores audit records (implemented by the Audit module). Called inside the request's
    /// transaction, so the records commit or roll back with the change they describe.
    /// </summary>
    public interface IAuditSink
    {
        Task WriteAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default);
        void Write(IReadOnlyList<AuditRecord> records);
    }
}
