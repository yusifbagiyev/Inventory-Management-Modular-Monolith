using Microsoft.EntityFrameworkCore;
using RouteService.Domain.Enums;
using RouteService.Infrastructure.Data;
using SharedServices.Contracts;

namespace RouteService.Infrastructure.Services
{
    /// <summary>Stores the WhatsApp delivery result of a transfer or a new product on its route, where the list shows it.</summary>
    public sealed class RouteWhatsAppStatus : IRouteWhatsAppStatus
    {
        private readonly RouteDbContext _context;

        public RouteWhatsAppStatus(RouteDbContext context) => _context = context;

        public async Task SetAsync(int routeId, string status, string? error, long? messageId = null, CancellationToken cancellationToken = default)
        {
            var route = await _context.InventoryRoutes.FirstOrDefaultAsync(r => r.Id == routeId, cancellationToken);
            if (route == null) return;
            route.SetWhatsAppStatus(status, error, messageId);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public Task<int?> FindEntryRouteIdAsync(int productId, CancellationToken cancellationToken = default)
            => _context.InventoryRoutes
                .Where(r => r.ProductSnapshot.ProductId == productId && (r.RouteType == RouteType.New || r.RouteType == RouteType.Existing))
                .OrderByDescending(r => r.Id)
                .Select(r => (int?)r.Id)
                .FirstOrDefaultAsync(cancellationToken);
    }
}
