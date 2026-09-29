using MediatR;

namespace SharedServices.Persistence
{
    /// <summary>
    /// Marks a MediatR request whose handler - together with everything it triggers in other
    /// modules (event handlers, nested commands) - must commit or roll back as one unit.
    /// </summary>
    public interface ITransactionalRequest { }

    public sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly DbSession _session;

        public TransactionBehavior(DbSession session) => _session = session;

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            // Nested requests join the outer transaction.
            if (request is not ITransactionalRequest || _session.InTransaction)
                return await next();

            await _session.BeginAsync(cancellationToken);
            try
            {
                var response = await next();
                await _session.CommitAsync(cancellationToken);
                return response;
            }
            catch
            {
                await _session.RollbackAsync();
                throw;
            }
        }
    }
}
