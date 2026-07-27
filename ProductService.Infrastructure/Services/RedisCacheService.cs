using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ProductService.Application.Interfaces;
using System.Text.Json;

namespace ProductService.Infrastructure.Services
{
    public class RedisCacheService : ICacheService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<RedisCacheService> _logger;

        public RedisCacheService(IDistributedCache cache, ILogger<RedisCacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                var cachedData = await _cache.GetStringAsync(key, cancellationToken);
                if (string.IsNullOrEmpty(cachedData))
                {
                    return null;
                }

                return JsonSerializer.Deserialize<T>(cachedData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving data from Redis cache for key: {Key}", key);
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                var serializedData = JsonSerializer.Serialize(value);
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(30)
                };

                await _cache.SetStringAsync(key, serializedData, options, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting data in Redis cache for key: {Key}", key);
            }
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                await _cache.RemoveAsync(key, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing data from Redis cache for key: {Key}", key);
            }
        }

        public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
        {
            // Note: Redis pattern-based deletion requires direct StackExchange.Redis access
            // For now, this is a placeholder - you can extend it if needed
            _logger.LogWarning("RemoveByPrefixAsync is not fully implemented. Pattern-based deletion requires direct Redis access.");
            await Task.CompletedTask;
        }
    }
}
