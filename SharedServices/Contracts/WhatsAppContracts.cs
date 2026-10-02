using SharedServices.Events;

namespace SharedServices.Contracts
{
    /// <summary>Where a transfer's WhatsApp group message stands.</summary>
    public static class WhatsAppStatus
    {
        public const string Queued = "Queued";
        public const string Sent = "Sent";
        public const string Failed = "Failed";
    }

    /// <summary>Records on a route how its WhatsApp message went.</summary>
    public interface IRouteWhatsAppStatus
    {
        Task SetAsync(int routeId, string status, string? error, CancellationToken cancellationToken = default);
    }

    /// <summary>Puts a completed transfer's WhatsApp message in the outbox again.</summary>
    public interface IWhatsAppRouteNotifier
    {
        /// <summary>False when WhatsApp is switched off or has no group configured.</summary>
        bool Enabled { get; }

        Task QueueRouteCompletedAsync(RouteCompletedEvent route, CancellationToken cancellationToken = default);
    }
}
