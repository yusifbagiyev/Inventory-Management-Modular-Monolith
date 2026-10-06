using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SharedServices.Persistence;

namespace SharedServices.Auditing
{
    /// <summary>Writes an audit record for every row a module creates, changes or deletes, in the same transaction.</summary>
    internal sealed class AuditInterceptor : SaveChangesInterceptor
    {
        private const int MaxValueLength = 500;

        /// <summary>How much unchanged text is kept before the first difference of two long values.</summary>
        private const int LeadLength = 60;

        /// <summary>Rows that are side effects or bookkeeping, not actions.</summary>
        private static readonly HashSet<string> IgnoredEntities = ["Notification", "RefreshToken", "AuditEntry"];

        /// <summary>Columns that change on their own, like sign-in stamps and concurrency tokens.</summary>
        private static readonly HashSet<string> IgnoredProperties =
        [
            "ConcurrencyStamp", "SecurityStamp", "LastLoginAt", "NormalizedUserName", "NormalizedEmail",
            "NormalizedName", "AccessFailedCount", "xmin", "UpdatedAt", "PasswordHash",
            "WhatsAppStatus", "WhatsAppError", "WhatsAppAt", "WhatsAppMessageId"
        ];

        /// <summary>Recorded as changed, never with their value.</summary>
        private static readonly HashSet<string> MaskedProperties = ["PasswordHash"];

        // Letters and signs like & are written as they are rather than escaped, so a list can be searched too
        private static readonly JsonSerializerOptions ListJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        private static readonly string[] LabelProperties = ["InventoryCode", "Model", "Name", "UserName", "RequestType"];

        private readonly AuditContext _audit;
        private readonly DbSession _session;
        private readonly IServiceProvider _services;
        private List<Pending>? _pending;
        private NpgsqlTransaction? _ownTransaction;
        private bool _watchingFailures;

        public AuditInterceptor(AuditContext audit, DbSession session, IServiceProvider services)
        {
            _audit = audit;
            _session = session;
            _services = services;
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Capture(eventData.Context);
            return result;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            // A transaction left by a save that never reported its end must not swallow this one
            await RollbackOwnTransactionAsync();
            Capture(eventData.Context);

            // Without a request-wide transaction EF would commit the change before its audit rows
            if (_pending is { Count: > 0 } && !_session.InTransaction && eventData.Context != null)
            {
                WatchFailures(eventData.Context);
                await _session.BeginAsync(cancellationToken);
                _session.Enlist(eventData.Context);
                _ownTransaction = _session.Transaction;
            }
            return result;
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            var records = TakeRecords();
            if (records.Count > 0)
                _services.GetRequiredService<IAuditSink>().Write(records);
            return result;
        }

        public override async ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            var records = TakeRecords();
            try
            {
                if (records.Count > 0)
                    await _services.GetRequiredService<IAuditSink>().WriteAsync(records, cancellationToken);
                if (_ownTransaction != null)
                {
                    _ownTransaction = null;
                    await _session.CommitAsync(cancellationToken);
                }
            }
            catch
            {
                await RollbackOwnTransactionAsync();
                throw;
            }
            return result;
        }

        public override void SaveChangesFailed(DbContextErrorEventData eventData) => _pending = null;

        public override async Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            _pending = null;
            await RollbackOwnTransactionAsync();
        }

        public override void SaveChangesCanceled(DbContextEventData eventData) => _pending = null;

        public override async Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
        {
            _pending = null;
            await RollbackOwnTransactionAsync();
        }

        /// <summary>Ends the own transaction after a concurrency conflict, which EF reports to interceptors only before it throws.</summary>
        private void WatchFailures(DbContext context)
        {
            if (_watchingFailures) return;
            _watchingFailures = true;
            context.SaveChangesFailed += (_, _) =>
            {
                _pending = null;
                if (_ownTransaction == null) return;
                // The event cannot be awaited, and left open the transaction would take in the request's later saves and undo them
                try { RollbackOwnTransactionAsync().GetAwaiter().GetResult(); }
                catch { /* The caller must see the save's own error */ }
            };
        }

        private async Task RollbackOwnTransactionAsync()
        {
            var own = _ownTransaction;
            if (own == null) return;
            _ownTransaction = null;
            // Only the transaction opened here, never a request-wide one that started after it ended
            if (ReferenceEquals(_session.Transaction, own))
                await _session.RollbackAsync();
        }

        private void Capture(DbContext? context)
        {
            _pending = null;
            if (context == null) return;

            // A copy, because labelling a deleted row looks through the tracked entries again
            foreach (var entry in context.ChangeTracker.Entries().ToList())
            {
                if (entry.Metadata.IsOwned() || IgnoredEntities.Contains(EntityName(entry)))
                    continue;

                List<AuditFieldChange> fields;
                string operation;
                switch (entry.State)
                {
                    case EntityState.Added:
                        operation = AuditOperations.Created;
                        fields = Fields(entry, "", (p, _) => (null, p.CurrentValue), includeUnchanged: true);
                        break;
                    case EntityState.Deleted:
                        operation = AuditOperations.Deleted;
                        fields = Fields(entry, "", (p, _) => (p.OriginalValue, null), includeUnchanged: true);
                        break;
                    case EntityState.Modified:
                        // A record that is kept and only marked as deleted is still a deletion to whoever reads the log
                        operation = SoftDelete.IsBeingDeleted(entry) ? AuditOperations.Deleted : AuditOperations.Updated;
                        fields = Fields(entry, "", (p, _) => (p.OriginalValue, p.CurrentValue), includeUnchanged: false);
                        // Identity marks every column on Update(), but a sign-in that only stamps LastLoginAt is no action
                        if (fields.Count == 0) continue;
                        break;
                    default:
                        continue;
                }

                // Deleted rows are labelled now, the rest after the save when generated ids exist
                var label = entry.State == EntityState.Deleted ? Label(entry) : null;
                (_pending ??= new()).Add(new Pending(entry, operation, fields, label));
            }
        }

        private List<AuditRecord> TakeRecords()
        {
            if (_pending is not { Count: > 0 } pending) return [];
            _pending = null;

            return pending.Select(p => _audit.Record(
                    EntityName(p.Entry),
                    Key(p.Entry),
                    p.Label ?? Label(p.Entry),
                    p.Operation,
                    p.Fields))
                .ToList();
        }

        /// <summary>The entry's columns and its owned parts' columns without keys, and for an update only real changes.</summary>
        private static List<AuditFieldChange> Fields(
            EntityEntry entry, string prefix,
            Func<PropertyEntry, EntityEntry, (object? Old, object? New)> values, bool includeUnchanged)
        {
            var result = new List<AuditFieldChange>();
            foreach (var property in entry.Properties)
            {
                var name = property.Metadata.Name;
                // A key that points at another record, like a role membership's user and role, says what the row links
                if (property.Metadata.IsPrimaryKey() && !property.Metadata.IsForeignKey()
                    || property.Metadata.IsShadowProperty() && property.Metadata.IsForeignKey())
                    continue;
                if (IgnoredProperties.Contains(name) && !MaskedProperties.Contains(name))
                    continue;
                if (!includeUnchanged && !property.IsModified)
                    continue;

                var (oldValue, newValue) = values(property, entry);
                // Whole values are compared, since a change may lie past the point where long text is cut
                var oldText = Format(oldValue);
                var newText = Format(newValue);
                if (oldText == newText || (includeUnchanged && oldText == null && newText == null))
                    continue;

                if (MaskedProperties.Contains(name))
                    result.Add(new AuditFieldChange(prefix + name, null, "(changed)"));
                else if (IsList(oldValue) || IsList(newValue))
                    result.Add(new AuditFieldChange(prefix + name, oldText, newText));
                else
                {
                    var (oldShown, newShown) = Cut(oldText, newText);
                    result.Add(new AuditFieldChange(prefix + name, oldShown, newShown));
                }
            }

            foreach (var reference in entry.References)
            {
                if (reference.TargetEntry is not { } owned || !reference.Metadata.TargetEntityType.IsOwned())
                    continue;
                var ownedChanged = owned.State is EntityState.Added or EntityState.Deleted or EntityState.Modified;
                if (!includeUnchanged && !ownedChanged)
                    continue;
                result.AddRange(Fields(owned, prefix + reference.Metadata.Name + ".", values,
                    includeUnchanged || owned.State != EntityState.Modified));
            }
            return result;
        }

        private static string EntityName(EntityEntry entry)
        {
            var name = entry.Metadata.ClrType.Name;
            var tick = name.IndexOf('`');
            return tick < 0 ? name : name[..tick];
        }

        private static string? Key(EntityEntry entry)
        {
            var key = entry.Metadata.FindPrimaryKey();
            if (key == null) return null;
            return string.Join(",", key.Properties.Select(p => Format(entry.Property(p.Name).CurrentValue)));
        }

        /// <summary>A short readable name for the row, such as a product's code and model.</summary>
        private static string? Label(EntityEntry entry)
        {
            var useOriginal = entry.State == EntityState.Deleted;
            var parts = new List<string>();

            void From(EntityEntry e)
            {
                foreach (var name in LabelProperties)
                {
                    if (e.Metadata.FindProperty(name) == null) continue;
                    var property = e.Property(name);
                    var text = Format(useOriginal ? property.OriginalValue : property.CurrentValue);
                    if (!string.IsNullOrWhiteSpace(text) && !parts.Contains(text))
                        parts.Add(text);
                }
            }

            From(entry);
            // Owned parts and loaded parents add to the label too
            foreach (var reference in entry.References)
            {
                if (reference.TargetEntry is { } target)
                    From(target);
            }
            // A row of ids alone, like a role membership, is named after the records it links when the save has them loaded
            if (parts.Count == 0)
            {
                foreach (var principal in LinkedEntries(entry))
                    From(principal);
            }
            return parts.Count == 0 ? null : Truncate(string.Join(" · ", parts.Take(3)));
        }

        /// <summary>The tracked records the entry's foreign keys point at, found without a query.</summary>
        private static IEnumerable<EntityEntry> LinkedEntries(EntityEntry entry)
        {
            // Users first, so a membership reads as the person and then the role
            var keys = entry.Metadata.GetForeignKeys()
                .OrderBy(k => k.PrincipalEntityType.ClrType.Name == "User" ? 0 : 1)
                .ToList();
            if (keys.Count == 0) yield break;

            var tracked = entry.Context.ChangeTracker.Entries().ToList();
            foreach (var key in keys)
            {
                var values = key.Properties.Select(p => entry.Property(p.Name).CurrentValue).ToArray();
                if (values.Any(v => v == null)) continue;
                var principal = tracked.FirstOrDefault(e => e.Entity != entry.Entity
                    && key.PrincipalEntityType.IsAssignableFrom(e.Metadata)
                    && key.PrincipalKey.Properties.Select(p => e.Property(p.Name).CurrentValue).SequenceEqual(values));
                if (principal != null)
                    yield return principal;
            }
        }

        /// <summary>The value as display text in full, where a list becomes JSON.</summary>
        private static string? Format(object? value) => value switch
        {
            null => null,
            string s => s,
            // MinValue means the date is not set yet
            DateTime d when d == DateTime.MinValue => null,
            DateTime d => d.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture),
            DateTimeOffset d => d.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            System.Enum e => e.ToString(),
            IEnumerable list => JsonSerializer.Serialize(list, ListJson),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };

        /// <summary>Lists are stored whole, because JSON cut in the middle can no longer be read as a list.</summary>
        private static bool IsList(object? value) => value is IEnumerable and not string;

        /// <summary>Cuts long text for storage, starting shortly before the first difference when that lies past the cut.</summary>
        private static (string? Old, string? New) Cut(string? oldText, string? newText)
        {
            if (oldText == null || newText == null)
                return (Truncate(oldText), Truncate(newText));

            var same = 0;
            var shorter = Math.Min(oldText.Length, newText.Length);
            while (same < shorter && oldText[same] == newText[same]) same++;
            if (same < MaxValueLength)
                return (Truncate(oldText), Truncate(newText));

            // Otherwise both stored values would be the same leading part and the change would not show
            var start = same - LeadLength;
            if (char.IsLowSurrogate(oldText[start])) start--;
            return ("…" + Truncate(oldText[start..]), "…" + Truncate(newText[start..]));
        }

        [return: NotNullIfNotNull(nameof(text))]
        private static string? Truncate(string? text)
        {
            if (text == null || text.Length <= MaxValueLength) return text;
            // A cut between the two halves of a surrogate pair would leave a broken character
            var length = char.IsHighSurrogate(text[MaxValueLength - 1]) ? MaxValueLength - 1 : MaxValueLength;
            return text[..length] + "…";
        }

        private sealed record Pending(EntityEntry Entry, string Operation, List<AuditFieldChange> Fields, string? Label);
    }
}
