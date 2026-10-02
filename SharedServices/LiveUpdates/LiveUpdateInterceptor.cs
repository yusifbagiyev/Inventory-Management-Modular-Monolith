using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SharedServices.Persistence;

namespace SharedServices.LiveUpdates
{
    public static class LiveUpdateServiceCollectionExtensions
    {
        /// <summary>Broadcasts committed changes to T under the given name.</summary>
        public static IServiceCollection TrackLiveEntity<T>(
            this IServiceCollection services, string name, string idProperty = "Id", params string[] ignoredProperties)
            => services.Configure<LiveUpdateOptions>(o => o.Track<T>(name, idProperty, ignoredProperties));
    }

    /// <summary>Announces the tracked entities a save touched once the transaction commits, never on rollback.</summary>
    internal sealed class LiveUpdateInterceptor : SaveChangesInterceptor
    {
        // Above this a single change with a null Id replaces the list, since pages refresh either way
        private const int MaxIdsPerKind = 20;

        private readonly DbSession _session;
        private readonly LiveUpdateOptions _options;
        private readonly IHttpContextAccessor? _httpContext;
        private List<Pending>? _pending;

        public LiveUpdateInterceptor(DbSession session, LiveUpdateOptions options, IHttpContextAccessor? httpContext)
        {
            _session = session;
            _options = options;
            _httpContext = httpContext;
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Capture(eventData.Context);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Capture(eventData.Context);
            return ValueTask.FromResult(result);
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            Publish();
            return result;
        }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            Publish();
            return ValueTask.FromResult(result);
        }

        public override void SaveChangesFailed(DbContextErrorEventData eventData) => _pending = null;

        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            _pending = null;
            return Task.CompletedTask;
        }

        private void Capture(DbContext? context)
        {
            _pending = null;
            if (context == null) return;

            foreach (var entry in context.ChangeTracker.Entries())
            {
                var action = entry.State switch
                {
                    EntityState.Added => "created",
                    EntityState.Modified => "updated",
                    EntityState.Deleted => "deleted",
                    _ => null
                };
                if (action == null || !_options.Entities.TryGetValue(entry.Metadata.ClrType, out var live))
                    continue;

                // Compared by value because Identity's Update() flags every column, even on a plain sign-in
                if (entry.State == EntityState.Modified && live.IgnoredProperties.Count > 0
                    && entry.Properties
                        .Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue))
                        .All(p => live.IgnoredProperties.Contains(p.Metadata.Name)))
                    continue;

                // New rows get their id from the database, so theirs is read after the save
                var id = entry.State == EntityState.Added ? null : ReadId(entry, live);
                (_pending ??= new()).Add(new Pending(entry, live, action, id));
            }
        }

        private void Publish()
        {
            if (_pending is not { Count: > 0 } pending) return;
            _pending = null;

            var changes = pending
                .Select(p => new EntityChange(p.Entity.Name, p.Id ?? ReadId(p.Entry, p.Entity), p.Action))
                .Distinct()
                .GroupBy(c => (c.Entity, c.Action))
                .SelectMany(g => g.Count() > MaxIdsPerKind
                    ? new[] { new EntityChange(g.Key.Entity, null, g.Key.Action) }
                    : g.AsEnumerable())
                .ToList();

            var user = _httpContext?.HttpContext?.User;
            var update = new LiveUpdate(changes, ActorId(user), ActorName(user), DateTime.Now);

            _session.AfterCommit((services, cancellationToken) =>
                services.GetService<ILiveUpdatePublisher>()?.PublishAsync(update, cancellationToken) ?? Task.CompletedTask);
        }

        private static int? ReadId(EntityEntry entry, LiveEntity live)
            => entry.Property(live.IdProperty).CurrentValue is int id ? id : null;

        private static int? ActorId(ClaimsPrincipal? user)
            => int.TryParse(user?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

        private static string? ActorName(ClaimsPrincipal? user)
        {
            if (user?.Identity?.IsAuthenticated != true) return null;
            var fullName = $"{user.FindFirst("FirstName")?.Value} {user.FindFirst("LastName")?.Value}".Trim();
            return fullName.Length > 0 ? fullName : user.Identity.Name;
        }

        private sealed record Pending(EntityEntry Entry, LiveEntity Entity, string Action, int? Id);
    }
}
