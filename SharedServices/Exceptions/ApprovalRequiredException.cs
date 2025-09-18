namespace SharedServices.Exceptions
{
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
        public DuplicateEntityException(string message) : base(message) { }
    }

    public class InsufficientPermissionsException : Exception
    {
        public InsufficientPermissionsException(string message) : base(message) { }
    }
}