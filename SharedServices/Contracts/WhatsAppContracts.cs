using SharedServices.Events;

namespace SharedServices.Contracts
{
    /// <summary>A transfer's WhatsApp group message: waiting in the outbox, delivered, or given up on.</summary>
    public static class WhatsAppStatus
    {
        public const string Queued = "Queued";
        public const string Sent = "Sent";
        public const string Failed = "Failed";
    }

    /// <summary>Records on a route how its WhatsApp message went (implemented by the Routes module).</summary>
    public interface IRouteWhatsAppStatus
    {
        Task SetAsync(int routeId, string status, string? error, CancellationToken cancellationToken = default);
    }

    /// <summary>Puts a completed transfer's WhatsApp message in the outbox again (implemented by Notifications).</summary>
    public interface IWhatsAppRouteNotifier
    {
        /// <summary>False when WhatsApp is switched off or has no group configured.</summary>
        bool Enabled { get; }

        Task QueueRouteCompletedAsync(RouteCompletedEvent route, CancellationToken cancellationToken = default);
    }
}
