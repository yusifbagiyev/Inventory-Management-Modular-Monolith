using SharedServices.Events;

namespace SharedServices.Contracts
{
    /// <summary>Where a route's WhatsApp group message stands.</summary>
    public static class WhatsAppStatus
    {
        public const string Queued = "Queued";
        public const string Sent = "Sent";
        public const string Failed = "Failed";
        /// <summary>Sent, then deleted from the group from the route pages.</summary>
        public const string Deleted = "Deleted";
    }

    /// <summary>Records on a route how its WhatsApp message went.</summary>
    public interface IRouteWhatsAppStatus
    {
        /// <summary>Stores the outcome, with the WaSender message id of a sent message so it can be deleted later.</summary>
        Task SetAsync(int routeId, string status, string? error, long? messageId = null, CancellationToken cancellationToken = default);

        /// <summary>Marks every message still queued as failed with the given error and returns how many there were.</summary>
        // A store that cannot list its queued messages leaves them as they are
        Task<int> FailQueuedAsync(string error, CancellationToken cancellationToken = default) => Task.FromResult(0);

        /// <summary>The route that recorded the product's creation, which carries the new product's WhatsApp message.</summary>
        Task<int?> FindEntryRouteIdAsync(int productId, CancellationToken cancellationToken = default);
    }

    /// <summary>Queues WhatsApp messages of routes again and deletes sent ones.</summary>
    public interface IWhatsAppRouteNotifier
    {
        /// <summary>False when WhatsApp is switched off or has no group configured.</summary>
        bool Enabled { get; }

        /// <summary>How long after sending a message can still be deleted for everyone in the group.</summary>
        TimeSpan DeleteWindow { get; }

        Task QueueRouteCompletedAsync(RouteCompletedEvent route, CancellationToken cancellationToken = default);

        /// <summary>Queues the new product message again, recording its outcome on the product's entry route.</summary>
        Task QueueProductCreatedAsync(ProductCreatedEvent product, int routeId, CancellationToken cancellationToken = default);

        /// <summary>Deletes a sent message from the group, returning null on success or the reason it failed.</summary>
        Task<string?> DeleteMessageAsync(long messageId, CancellationToken cancellationToken = default);
    }
}
