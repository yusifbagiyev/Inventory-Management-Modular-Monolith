# Redis Integration Guide for ProductService

## Overview
This guide explains how to enable and test Redis caching in ProductService without modifying your existing codebase.

## Features
- **Toggle-based**: Enable/disable Redis via `appsettings.json`
- **Fallback mechanism**: Uses `NoCacheService` when Redis is disabled (no-op)
- **Zero breaking changes**: Existing code works unchanged
- **Performance testing ready**: Easy to compare with/without Redis

---

## How to Enable Redis

### 1. Update appsettings.json

Edit `ProductService.API/appsettings.json`:

```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "your-redis-host:6379,password=your-password,ssl=false,abortConnect=false"
  }
}
```

Replace `your-redis-host:6379` with your actual Redis connection string.

**Connection String Examples:**
- Local: `localhost:6379`
- Remote without auth: `192.168.1.100:6379,abortConnect=false`
- With password: `redis.example.com:6379,password=mypassword,ssl=true,abortConnect=false`
- Azure Redis: `myredis.redis.cache.windows.net:6380,password=key,ssl=true,abortConnect=false`

### 2. Enable Cached Repository (Optional)

To use the cached version of ProductRepository, update `DependencyInjection.cs`:

**Option A: Replace ProductRepository with CachedProductRepository**

```csharp
// In DependencyInjection.cs, replace:
services.AddScoped<IProductRepository, ProductRepository>();

// With:
services.AddScoped<ProductRepository>(); // Register inner repository
services.AddScoped<IProductRepository, CachedProductRepository>();
```

**Option B: Keep existing and add caching selectively**

Keep `ProductRepository` as-is and inject `ICacheService` directly into your handlers/controllers for selective caching.

---

## How to Disable Redis

Simply set `Enabled: false` in appsettings.json:

```json
{
  "Redis": {
    "Enabled": false,
    "ConnectionString": "..."
  }
}
```

The application will use `NoCacheService` (no-op) and work exactly as before.

---

## Testing Redis Performance

### 1. Baseline Test (Redis Disabled)

```json
"Redis": { "Enabled": false }
```

Run your load tests and measure:
- API response times
- Database query counts
- Throughput (requests/second)

### 2. Redis Enabled Test

```json
"Redis": { "Enabled": true, "ConnectionString": "localhost:6379" }
```

Run the same load tests and compare metrics.

### 3. Monitor Redis

Use Redis CLI to monitor cache hits:

```bash
redis-cli
> MONITOR
```

Or check specific keys:

```bash
redis-cli
> KEYS ProductService_*
> GET ProductService_product:id:123
> TTL ProductService_product:id:123
```

---

## Cache Configuration

### Default Cache Expiration Times

| Cache Type | TTL | Location |
|------------|-----|----------|
| Product by ID | 15 minutes | CachedProductRepository.cs:17 |
| Product lists | 15 minutes | CachedProductRepository.cs:17 |
| Paginated queries | 5 minutes | CachedProductRepository.cs:121 |

To change expiration times, edit `CachedProductRepository.cs`:

```csharp
private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(30); // Change this
```

### Cache Key Pattern

All keys use the format: `ProductService_product:{type}:{value}`

Examples:
- `ProductService_product:id:123`
- `ProductService_product:code:45678`
- `ProductService_product:category:5`
- `ProductService_product:department:2`

---

## Using ICacheService Directly

You can inject `ICacheService` into any service/handler for custom caching:

```csharp
public class YourHandler
{
    private readonly ICacheService _cache;

    public YourHandler(ICacheService cache)
    {
        _cache = cache;
    }

    public async Task<ProductDto> Handle(GetProductQuery request, CancellationToken ct)
    {
        var cacheKey = $"custom:product:{request.Id}";

        // Try cache first
        var cached = await _cache.GetAsync<ProductDto>(cacheKey, ct);
        if (cached != null) return cached;

        // Fetch from database
        var product = await _repository.GetByIdAsync(request.Id, ct);
        var dto = _mapper.Map<ProductDto>(product);

        // Store in cache
        await _cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10), ct);

        return dto;
    }
}
```

---

## Troubleshooting

### Redis Connection Fails

**Symptom:** Application throws exceptions on startup

**Solution:**
1. Check if Redis is running: `redis-cli ping` (should return "PONG")
2. Verify connection string in appsettings.json
3. Check firewall rules
4. Set `abortConnect=false` in connection string to allow startup even if Redis is down

### No Performance Improvement

**Possible reasons:**
1. Redis is not enabled (check console output on startup)
2. CachedProductRepository is not registered
3. Query patterns don't match cached scenarios (e.g., complex filtered queries)
4. Cache TTL is too short
5. Network latency to Redis server

**Debug steps:**
- Check console output: Should see "Redis caching is ENABLED for ProductService"
- Monitor Redis: Use `redis-cli MONITOR` to see cache operations
- Add logging in `RedisCacheService.cs` to track cache hits/misses

### Cache Invalidation Issues

**Symptom:** Stale data returned after updates

**Solution:**
- Use `CachedProductRepository` which automatically invalidates on write operations
- Manually call `_cache.RemoveAsync(key)` after updates
- Reduce cache TTL for frequently changing data

---

## Performance Tips

1. **Cache read-heavy operations only**: Don't cache if data changes frequently
2. **Avoid caching large result sets**: Paginated queries with filters should skip cache
3. **Use shorter TTL for list queries**: Single items can have longer TTL (15min) vs lists (5min)
4. **Monitor memory usage**: Check Redis memory consumption
5. **Use Redis on same network**: Network latency matters

---

## Next Steps

1. **Test with your Redis instance**: Update connection string and enable Redis
2. **Run performance benchmarks**: Compare enabled vs disabled
3. **Monitor cache hit ratio**: Use Redis INFO stats
4. **Tune cache expiration**: Adjust TTL based on your data update patterns
5. **Scale if needed**: Consider Redis Cluster for high availability

---

## Files Modified/Created

- `ProductService.Infrastructure.csproj` - Added Redis NuGet packages
- `ProductService.Application/Interfaces/ICacheService.cs` - Cache service interface
- `ProductService.Infrastructure/Services/RedisCacheService.cs` - Redis implementation
- `ProductService.Infrastructure/Services/NoCacheService.cs` - No-op fallback
- `ProductService.Infrastructure/Repositories/CachedProductRepository.cs` - Example cached repository
- `ProductService.Infrastructure/DependencyInjection.cs` - Conditional Redis registration
- `ProductService.API/appsettings.json` - Redis configuration

---

## Questions?

The integration is designed to be:
- **Safe**: Fallback to no-cache if Redis fails
- **Non-invasive**: No changes to existing code required
- **Testable**: Easy to toggle on/off for A/B testing
- **Production-ready**: Proper error handling and logging
