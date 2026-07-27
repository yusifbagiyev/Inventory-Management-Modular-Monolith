using ProductService.Application.Interfaces;
using ProductService.Domain.Common;
using ProductService.Domain.Entities;
using ProductService.Domain.Repositories;

namespace ProductService.Infrastructure.Repositories
{
    /// <summary>
    /// Cached wrapper for ProductRepository. Demonstrates Redis caching for database queries.
    /// If Redis is disabled, ICacheService will be NoCacheService (no-op), so behavior is unchanged.
    /// </summary>
    public class CachedProductRepository : IProductRepository
    {
        private readonly IProductRepository _innerRepository;
        private readonly ICacheService _cacheService;
        private const string CacheKeyPrefix = "product:";
        private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(15);

        public CachedProductRepository(
            ProductRepository innerRepository,
            ICacheService cacheService)
        {
            _innerRepository = innerRepository;
            _cacheService = cacheService;
        }

        public async Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"{CacheKeyPrefix}id:{id}";

            // Try to get from cache first
            var cachedProduct = await _cacheService.GetAsync<Product>(cacheKey, cancellationToken);
            if (cachedProduct != null)
            {
                return cachedProduct;
            }

            // Cache miss - get from database
            var product = await _innerRepository.GetByIdAsync(id, cancellationToken);

            // Store in cache if found
            if (product != null)
            {
                await _cacheService.SetAsync(cacheKey, product, CacheExpiration, cancellationToken);
            }

            return product;
        }

        public async Task<Product?> GetByInventoryCodeAsync(int inventoryCode, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"{CacheKeyPrefix}code:{inventoryCode}";

            var cachedProduct = await _cacheService.GetAsync<Product>(cacheKey, cancellationToken);
            if (cachedProduct != null)
            {
                return cachedProduct;
            }

            var product = await _innerRepository.GetByInventoryCodeAsync(inventoryCode, cancellationToken);

            if (product != null)
            {
                await _cacheService.SetAsync(cacheKey, product, CacheExpiration, cancellationToken);
            }

            return product;
        }

        public async Task<IEnumerable<Product>> GetByCategoryIdAsync(int categoryId, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"{CacheKeyPrefix}category:{categoryId}";

            var cachedProducts = await _cacheService.GetAsync<IEnumerable<Product>>(cacheKey, cancellationToken);
            if (cachedProducts != null)
            {
                return cachedProducts;
            }

            var products = await _innerRepository.GetByCategoryIdAsync(categoryId, cancellationToken);
            await _cacheService.SetAsync(cacheKey, products, CacheExpiration, cancellationToken);

            return products;
        }

        public async Task<IEnumerable<Product>> GetByDepartmentIdAsync(int departmentId, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"{CacheKeyPrefix}department:{departmentId}";

            var cachedProducts = await _cacheService.GetAsync<IEnumerable<Product>>(cacheKey, cancellationToken);
            if (cachedProducts != null)
            {
                return cachedProducts;
            }

            var products = await _innerRepository.GetByDepartmentIdAsync(departmentId, cancellationToken);
            await _cacheService.SetAsync(cacheKey, products, CacheExpiration, cancellationToken);

            return products;
        }

        // Paginated queries typically should NOT be cached due to high cardinality of cache keys
        // But we can cache them with shorter TTL if needed
        public async Task<PagedResult<Product>> GetAllAsync(
            int pageNumber,
            int pageSize,
            string? search,
            DateTime? startDate,
            DateTime? endDate,
            bool? status,
            bool? availability,
            int? categoryId = null,
            int? departmentId = null,
            bool? hasImage = null,
            bool? assigned = null,
            CancellationToken cancellationToken = default)
        {
            // For demo purposes, we'll cache only simple queries (no search, no filters)
            // In production, you'd want to be more selective about what to cache
            if (string.IsNullOrEmpty(search) && !startDate.HasValue && !endDate.HasValue &&
                !status.HasValue && !availability.HasValue && !categoryId.HasValue && !departmentId.HasValue &&
                !hasImage.HasValue && !assigned.HasValue)
            {
                var cacheKey = $"{CacheKeyPrefix}all:page:{pageNumber}:size:{pageSize}";

                var cachedResult = await _cacheService.GetAsync<PagedResult<Product>>(cacheKey, cancellationToken);
                if (cachedResult != null)
                {
                    return cachedResult;
                }

                var result = await _innerRepository.GetAllAsync(
                    pageNumber, pageSize, search, startDate, endDate, status, availability, categoryId, departmentId, hasImage, assigned, cancellationToken);

                // Cache for shorter duration (5 minutes) for list queries
                await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5), cancellationToken);

                return result;
            }

            // Complex queries with filters - skip cache
            return await _innerRepository.GetAllAsync(
                pageNumber, pageSize, search, startDate, endDate, status, availability, categoryId, departmentId, hasImage, assigned, cancellationToken);
        }

        public async Task<IReadOnlyList<(int DepartmentId, int CategoryId)>> GetDepartmentCategoryPairsAsync(
            bool? status = null,
            bool? availability = null,
            bool? hasImage = null,
            bool? assigned = null,
            CancellationToken cancellationToken = default)
        {
            // Only the unfiltered facet set is cached; a filtered request (cascading against the
            // active state filters) is cheap and varies too much to be worth caching.
            var noFilters = !status.HasValue && !availability.HasValue && !hasImage.HasValue && !assigned.HasValue;
            if (!noFilters)
            {
                return await _innerRepository.GetDepartmentCategoryPairsAsync(
                    status, availability, hasImage, assigned, cancellationToken);
            }

            var cacheKey = $"{CacheKeyPrefix}facets:dept-category";

            var cached = await _cacheService.GetAsync<List<int[]>>(cacheKey, cancellationToken);
            if (cached != null)
            {
                return cached.Select(a => (a[0], a[1])).ToList();
            }

            var pairs = await _innerRepository.GetDepartmentCategoryPairsAsync(cancellationToken: cancellationToken);

            // ValueTuple has no stable JSON shape, so cache as int[] pairs (invalidated on every write).
            await _cacheService.SetAsync(
                cacheKey,
                pairs.Select(p => new[] { p.DepartmentId, p.CategoryId }).ToList(),
                TimeSpan.FromMinutes(5),
                cancellationToken);

            return pairs;
        }

        // Write operations - invalidate cache
        public async Task<Product> AddAsync(Product product, CancellationToken cancellationToken = default)
        {
            var result = await _innerRepository.AddAsync(product, cancellationToken);

            // Invalidate relevant caches
            await InvalidateCachesAsync(product, cancellationToken);

            return result;
        }

        public async Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
        {
            await _innerRepository.UpdateAsync(product, cancellationToken);

            // Invalidate relevant caches
            await InvalidateCachesAsync(product, cancellationToken);
        }

        public async Task DeleteAsync(Product product, CancellationToken cancellationToken = default)
        {
            await _innerRepository.DeleteAsync(product, cancellationToken);

            // Invalidate relevant caches
            await InvalidateCachesAsync(product, cancellationToken);
        }

        public Task<bool> ExistsByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            // Existence checks can use cached data
            return _innerRepository.ExistsByIdAsync(id, cancellationToken);
        }

        // Deliberately NOT cached: these counts gate department/category deletion, so a stale
        // count could let a delete through that the foreign key would then reject.
        public Task<int> CountByDepartmentIdAsync(int departmentId, CancellationToken cancellationToken = default)
        {
            return _innerRepository.CountByDepartmentIdAsync(departmentId, cancellationToken);
        }

        public Task<int> CountByCategoryIdAsync(int categoryId, CancellationToken cancellationToken = default)
        {
            return _innerRepository.CountByCategoryIdAsync(categoryId, cancellationToken);
        }

        private async Task InvalidateCachesAsync(Product product, CancellationToken cancellationToken)
        {
            // Remove specific product caches
            await _cacheService.RemoveAsync($"{CacheKeyPrefix}id:{product.Id}", cancellationToken);
            await _cacheService.RemoveAsync($"{CacheKeyPrefix}code:{product.InventoryCode}", cancellationToken);

            // Remove category and department lists
            await _cacheService.RemoveAsync($"{CacheKeyPrefix}category:{product.CategoryId}", cancellationToken);
            await _cacheService.RemoveAsync($"{CacheKeyPrefix}department:{product.DepartmentId}", cancellationToken);

            // Adding/moving/removing a product can change which (department, category) pairs exist.
            await _cacheService.RemoveAsync($"{CacheKeyPrefix}facets:dept-category", cancellationToken);

            // Note: In production, you might want to use Redis SCAN with pattern matching
            // to invalidate all "product:all:*" keys, but that requires direct Redis access
        }
    }
}
