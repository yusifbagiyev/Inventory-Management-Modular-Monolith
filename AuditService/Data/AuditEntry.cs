namespace AuditService.Data
{
    /// <summary>
    /// One row of the audit log: one created, changed or deleted record (or a sign-in event).
    /// Rows written by one request share <see cref="CorrelationId"/> and form one action.
    /// </summary>
    public class AuditEntry
    {
        public long Id { get; set; }
        public DateTime At { get; set; }
        public string CorrelationId { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public string? UserName { get; set; }
        public string? IpAddress { get; set; }
        /// <summary>The command or endpoint ("UpdateProduct", "UserManagement.Edit").</summary>
        public string Action { get; set; } = string.Empty;
        /// <summary>"Product", "InventoryRoute", "User"...</summary>
        public string EntityType { get; set; } = string.Empty;
        public string? EntityId { get; set; }
        /// <summary>A readable name for the record at the time ("1001 · Latitude 5420").</summary>
        public string? Label { get; set; }
        /// <summary>Created, Updated, Deleted, SignedIn, SignInFailed, SignedOut.</summary>
        public string Operation { get; set; } = string.Empty;
        /// <summary>JSON array of {field, old, new}.</summary>
        public string Changes { get; set; } = "[]";
    }
}
