namespace SharedServices.Contracts
{
    /// <summary>Read access to users for other modules.</summary>
    public interface IUserDirectory
    {
        /// <summary>Admins plus every active user granted the permission.</summary>
        Task<IReadOnlyList<int>> GetActiveUserIdsWithPermissionAsync(string permission, CancellationToken cancellationToken = default);
    }
}
