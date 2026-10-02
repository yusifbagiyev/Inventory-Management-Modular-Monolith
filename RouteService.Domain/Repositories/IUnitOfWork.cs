namespace RouteService.Domain.Repositories
{
    /// <remarks>The host's transaction behavior opens the transaction, so commands only save.</remarks>
    public interface IUnitOfWork
    {
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
