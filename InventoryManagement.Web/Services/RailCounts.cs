using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ProductService.Application.Features.Products.Queries;

namespace InventoryManagement.Web.Services
{
    /// <summary>
    /// The product total next to "Products" in the sidebar. Every page renders the sidebar, so
    /// the count is cached for a minute instead of costing a query per page.
    /// </summary>
    public sealed class RailCounts
    {
        private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);

        private readonly IMemoryCache _cache;
        private readonly IMediator _mediator;
        private readonly ILogger<RailCounts> _logger;

        public RailCounts(IMemoryCache cache, IMediator mediator, ILogger<RailCounts> logger)
        {
            _cache = cache;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<int?> ProductTotalAsync()
        {
            try
            {
                return await _cache.GetOrCreateAsync("rail:product-total", async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = Lifetime;
                    return (await _mediator.Send(new GetProductCountsQuery())).Total;
                });
            }
            catch (Exception ex)
            {
                // A count in the menu is not worth failing the page for.
                _logger.LogWarning(ex, "Could not read the product total for the sidebar");
                return null;
            }
        }
    }
}
