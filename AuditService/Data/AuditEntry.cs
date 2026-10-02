namespace AuditService.Data
{
    /// <summary>One audit log row. Rows of one request share a CorrelationId and form one action.</summary>
    public class AuditEntry
    {
        public long Id { get; set; }
        public DateTime At { get; set; }
        public string CorrelationId { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public string? UserName { get; set; }
        public string? IpAddress { get; set; }
        /// <summary>The command name, or Controller.Action when no command ran.</summary>
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string? EntityId { get; set; }
        /// <summary>A readable name for the record as it was at the time.</summary>
        public string? Label { get; set; }
        /// <summary>One of the AuditOperations values.</summary>
        public string Operation { get; set; } = string.Empty;
        /// <summary>JSON array of {field, old, new}.</summary>
        public string Changes { get; set; } = "[]";
    }
}
