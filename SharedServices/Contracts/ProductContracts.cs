namespace SharedServices.Contracts
{
    public record ProductSummary(
        int Id,
        int InventoryCode,
        string Model,
        string Vendor,
        int CategoryId,
        string CategoryName,
        int DepartmentId,
        string DepartmentName,
        bool IsWorking,
        string? Worker,
        string? ImageUrl);

    public record DepartmentSummary(int Id, string Name, bool IsActive = true);

    /// <summary>Read access to the products module for other modules.</summary>
    public interface IProductCatalog
    {
        Task<ProductSummary?> GetProductAsync(int productId, CancellationToken cancellationToken = default);
        Task<DepartmentSummary?> GetDepartmentAsync(int departmentId, CancellationToken cancellationToken = default);
    }

    /// <summary>Write access used by the routes module when a transfer completes.</summary>
    public interface IProductTransfers
    {
        /// <summary>
        /// Moves the product to <paramref name="toDepartmentId"/>/<paramref name="toWorker"/>. When the
        /// route has images, the product's images are replaced by copies of them.
        /// Runs in the caller's transaction.
        /// </summary>
        Task ApplyTransferAsync(
            int productId,
            int toDepartmentId,
            string? toWorker,
            IReadOnlyList<string> routeImageUrls,
            CancellationToken cancellationToken = default);
    }
}
