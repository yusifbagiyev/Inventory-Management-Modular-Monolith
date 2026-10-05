using ApprovalService.Application.Features.Queries;
using InventoryManagement.Web.Services.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ProductService.Application.Features.Products.Queries;

namespace InventoryManagement.Web.Services
{
    /// <summary>Counts every page shows in the menu and the toolbar, read with the page instead of by later requests.</summary>
    public sealed class RailCounts
    {
        private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);

        private readonly IMemoryCache _cache;
        private readonly IMediator _mediator;
        private readonly INotificationService _notifications;
        private readonly ILogger<RailCounts> _logger;

        public RailCounts(IMemoryCache cache, IMediator mediator, INotificationService notifications, ILogger<RailCounts> logger)
        {
            _cache = cache;
            _mediator = mediator;
            _notifications = notifications;
            _logger = logger;
        }

        /// <summary>The signed-in user's unread notifications, or null when the page should ask for it later.</summary>
        public async Task<int?> UnreadAsync()
        {
            try
            {
                return await _notifications.GetUnreadCountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read the unread count for the toolbar");
                return null;
            }
        }

        /// <summary>Approval requests waiting for a decision, not cached since a decision changes it at once.</summary>
        public async Task<int?> PendingApprovalsAsync()
        {
            try
            {
                return (await _mediator.Send(new GetPendingRequests.Query(1, 1))).TotalCount;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read the pending approvals for the sidebar");
                return null;
            }
        }

        /// <summary>Sidebar product total, cached for a minute because every page renders the sidebar.</summary>
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
                // A count in the menu is not worth failing the page for
                _logger.LogWarning(ex, "Could not read the product total for the sidebar");
                return null;
            }
        }
    }
}
