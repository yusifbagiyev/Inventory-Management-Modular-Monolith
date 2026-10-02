using MediatR;

namespace SharedServices.Persistence
{
    /// <summary>Marks a request that commits or rolls back as one unit with everything it triggers.</summary>
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
