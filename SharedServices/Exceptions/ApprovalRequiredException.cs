namespace SharedServices.Exceptions
{
    /// <summary>Thrown when an action was submitted for approval instead of being run, answered with 202.</summary>
    public class ApprovalRequiredException : Exception
    {
        public int ApprovalRequestId { get; }
        public string Status { get; }

        public ApprovalRequiredException(int approvalRequestId, string message)
            : base(message)
        {
            ApprovalRequestId = approvalRequestId;
            Status = "PendingApproval";
        }
    }

    public class DuplicateEntityException : Exception
    {
        /// <summary>The form field holding the taken value, so the page can show the message under it.</summary>
        public string? Field { get; }

        public DuplicateEntityException(string message, string? field = null) : base(message)
        {
            Field = field;
        }
    }

    public class InsufficientPermissionsException : Exception
    {
        public InsufficientPermissionsException(string message) : base(message) { }
    }
}