namespace SharedServices.LiveUpdates
{
    /// <summary>One committed change as browsers see it. Action is created, updated or deleted.</summary>
    public sealed record EntityChange(string Entity, int? Id, string Action);

    // No record data on purpose. Pages re-fetch, so each viewer only sees what their permissions allow.
    /// <summary>The changes one save committed and who made them.</summary>
    public sealed record LiveUpdate(IReadOnlyList<EntityChange> Changes, int? ActorId, string? ActorName, DateTime At);

    /// <summary>Delivers live updates to connected browsers.</summary>
    public interface ILiveUpdatePublisher
    {
        Task PublishAsync(LiveUpdate update, CancellationToken cancellationToken);
    }

    /// <summary>Entity types that are broadcast. Each module adds its own with TrackLiveEntity.</summary>
    public sealed class LiveUpdateOptions
    {
        internal Dictionary<Type, LiveEntity> Entities { get; } = new();

        // An update that touches only ignoredProperties, such as a last-login stamp, is not broadcast.
        public LiveUpdateOptions Track<T>(string name, string idProperty = "Id", params string[] ignoredProperties)
        {
            Entities[typeof(T)] = new LiveEntity(name, idProperty, new HashSet<string>(ignoredProperties));
            return this;
        }
    }

    internal sealed record LiveEntity(string Name, string IdProperty, HashSet<string> IgnoredProperties);
}
