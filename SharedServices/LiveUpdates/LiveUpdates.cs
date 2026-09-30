namespace SharedServices.LiveUpdates
{
    /// <summary>One committed change as browsers see it: which kind of record, which one, what happened.</summary>
    /// <param name="Entity">Name the module registered for the type ("product", "route", ...).</param>
    /// <param name="Action">"created", "updated" or "deleted".</param>
    public sealed record EntityChange(string Entity, int? Id, string Action);

    /// <summary>
    /// The changes one SaveChanges committed, and who made them. Carries no record data: open pages
    /// re-fetch themselves, so every viewer still only sees what their own permissions allow.
    /// </summary>
    public sealed record LiveUpdate(IReadOnlyList<EntityChange> Changes, int? ActorId, string? ActorName, DateTime At);

    /// <summary>Delivers <see cref="LiveUpdate"/>s to connected browsers (the notification module's hub).</summary>
    public interface ILiveUpdatePublisher
    {
        Task PublishAsync(LiveUpdate update, CancellationToken cancellationToken);
    }

    /// <summary>Which entity types are broadcast, filled by each module (<c>services.TrackLiveEntity</c>).</summary>
    public sealed class LiveUpdateOptions
    {
        internal Dictionary<Type, LiveEntity> Entities { get; } = new();

        /// <param name="name">Name sent to browsers; pages subscribe by it.</param>
        /// <param name="idProperty">Property holding the id reported for the change (e.g. "UserId" on a join row).</param>
        /// <param name="ignoredProperties">An update touching only these is not broadcast (e.g. a last-login stamp).</param>
        public LiveUpdateOptions Track<T>(string name, string idProperty = "Id", params string[] ignoredProperties)
        {
            Entities[typeof(T)] = new LiveEntity(name, idProperty, new HashSet<string>(ignoredProperties));
            return this;
        }
    }

    internal sealed record LiveEntity(string Name, string IdProperty, HashSet<string> IgnoredProperties);
}
