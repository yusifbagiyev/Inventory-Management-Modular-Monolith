using MediatR;
using SharedServices.Persistence;

namespace SharedServices.Auditing
{
    /// <summary>Names the audited action after the first command, so nested commands are recorded under it.</summary>
    public sealed class AuditActionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly AuditContext _audit;

        public AuditActionBehavior(AuditContext audit) => _audit = audit;

        public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (_audit.Action == null && IsCommand(typeof(TRequest)))
                _audit.Action = ActionName(typeof(TRequest));
            return next();
        }

        private static bool IsCommand(Type type)
            => typeof(ITransactionalRequest).IsAssignableFrom(type) || type.Name.EndsWith("Command", StringComparison.Ordinal);

        // A nested Command takes its outer class name. A trailing Command suffix is dropped.
        private static string ActionName(Type type)
        {
            var name = type.Name is "Command" && type.DeclaringType != null ? type.DeclaringType.Name : type.Name;
            return name.EndsWith("Command", StringComparison.Ordinal) && name.Length > "Command".Length
                ? name[..^"Command".Length]
                : name;
        }
    }
}
