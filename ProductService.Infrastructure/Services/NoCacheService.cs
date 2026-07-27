using Microsoft.Extensions.Logging;
using ProductService.Application.Interfaces;

namespace ProductService.Infrastructure.Services
{
    /// <summary>
    /// No-op cache service that does nothing. Used when Redis is disabled.
    /// </summary>
    public class NoCacheService : ICacheService
    {
        private readonly ILogger<NoCacheService> _logger;

        public NoCacheService(ILogger<NoCacheService> logger)
        {
            _logger = logger;
        }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            // Always return null (cache miss)
            return Task.FromResult<T?>(null);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
        {
            // Do nothing
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            // Do nothing
            return Task.CompletedTask;
        }

        public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
        {
            // Do nothing
            return Task.CompletedTask;
        }
    }
}
