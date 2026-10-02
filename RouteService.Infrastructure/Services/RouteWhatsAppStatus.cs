using Microsoft.EntityFrameworkCore;
using RouteService.Infrastructure.Data;
using SharedServices.Contracts;

namespace RouteService.Infrastructure.Services
{
    /// <summary>Stores a transfer's WhatsApp delivery result on its route, where the list shows failures.</summary>
    public sealed class RouteWhatsAppStatus : IRouteWhatsAppStatus
    {
        private readonly RouteDbContext _context;

        public RouteWhatsAppStatus(RouteDbContext context) => _context = context;

        public async Task SetAsync(int routeId, string status, string? error, CancellationToken cancellationToken = default)
        {
            var route = await _context.InventoryRoutes.FirstOrDefaultAsync(r => r.Id == routeId, cancellationToken);
            if (route == null) return;
            route.SetWhatsAppStatus(status, error);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
