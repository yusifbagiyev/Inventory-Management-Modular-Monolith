namespace RouteService.Domain.Repositories
{
    /// <summary>Commands only save, since the host's transaction behavior opens the transaction.</summary>
    public interface IUnitOfWork
    {
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
