namespace SharedServices.Contracts
{
    /// <summary>Read access to users for other modules (e.g. notification fan-out).</summary>
    public interface IUserDirectory
    {
        /// <summary>Ids of all active users that hold at least one role.</summary>
        Task<IReadOnlyList<int>> GetActiveUserIdsAsync(CancellationToken cancellationToken = default);

        /// <summary>Ids of active users in <paramref name="role"/>.</summary>
        Task<IReadOnlyList<int>> GetActiveUserIdsInRoleAsync(string role, CancellationToken cancellationToken = default);
    }
}
