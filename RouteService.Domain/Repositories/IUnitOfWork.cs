namespace RouteService.Domain.Repositories
{
    /// <remarks>
    /// Transactions are opened by the host's MediatR transaction behavior, which also covers the
    /// products module when a route completes; commands only save.
    /// </remarks>
    public interface IUnitOfWork
    {
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
