namespace ProductService.Domain.Common
{
    /// <summary>Column filters and sort of the product and deleted product lists, where a null or empty field means no filter.</summary>
    public record ProductListFilter
    {
        /// <summary>code, product, category, department, worker, updated, state, deleted or deletedBy; null keeps the list's own order.</summary>
        public string? Sort { get; init; }
        public bool Descending { get; init; }
        public int[]? CategoryIds { get; init; }
        public int[]? DepartmentIds { get; init; }
        /// <summary>Part of the inventory code.</summary>
        public string? Code { get; init; }
        /// <summary>Part of the model or the vendor.</summary>
        public string? Product { get; init; }
        public string? Worker { get; init; }
        /// <summary>Exact values picked from a column's list of the values in use.</summary>
        public int[]? Codes { get; init; }
        public string[]? Models { get; init; }
        public string[]? Workers { get; init; }
        /// <summary>Last change, or creation for a product never changed, both days inclusive.</summary>
        public DateTime? UpdatedFrom { get; init; }
        public DateTime? UpdatedTo { get; init; }
        public DateTime? DeletedFrom { get; init; }
        public DateTime? DeletedTo { get; init; }
        public string[]? DeletedBy { get; init; }

        public bool HasText => !string.IsNullOrWhiteSpace(Code) || !string.IsNullOrWhiteSpace(Product) || !string.IsNullOrWhiteSpace(Worker);
    }
}
