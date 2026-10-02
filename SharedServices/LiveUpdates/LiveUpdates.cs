namespace SharedServices.LiveUpdates
{
    /// <summary>One committed change as browsers see it, where Action is created, updated or deleted.</summary>
    public sealed record EntityChange(string Entity, int? Id, string Action);

    /// <summary>The changes one save committed and who made them, with no record data so pages re-fetch per viewer.</summary>
    public sealed record LiveUpdate(IReadOnlyList<EntityChange> Changes, int? ActorId, string? ActorName, DateTime At);

    /// <summary>Delivers live updates to connected browsers.</summary>
    public interface ILiveUpdatePublisher
    {
        Task PublishAsync(LiveUpdate update, CancellationToken cancellationToken);
    }

    /// <summary>Entity types that are broadcast, which each module adds with TrackLiveEntity.</summary>
    public sealed class LiveUpdateOptions
    {
        internal Dictionary<Type, LiveEntity> Entities { get; } = new();

        /// <summary>Tracks T, skipping updates that only touch ignoredProperties like a last-login stamp.</summary>
        public LiveUpdateOptions Track<T>(string name, string idProperty = "Id", params string[] ignoredProperties)
        {
            Entities[typeof(T)] = new LiveEntity(name, idProperty, new HashSet<string>(ignoredProperties));
            return this;
        }
    }

    internal sealed record LiveEntity(string Name, string IdProperty, HashSet<string> IgnoredProperties);
}
