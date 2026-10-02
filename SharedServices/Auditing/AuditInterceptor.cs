using System.Collections;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SharedServices.Persistence;

namespace SharedServices.Auditing
{
    /// <summary>Writes an audit record for every row a module creates, changes or deletes, in the same transaction.</summary>
    internal sealed class AuditInterceptor : SaveChangesInterceptor
    {
        private const int MaxValueLength = 500;

        /// <summary>Rows that are side effects or bookkeeping, not actions.</summary>
        private static readonly HashSet<string> IgnoredEntities = ["Notification", "RefreshToken", "AuditEntry"];

        /// <summary>Columns that change on their own, like sign-in stamps and concurrency tokens.</summary>
        private static readonly HashSet<string> IgnoredProperties =
        [
            "ConcurrencyStamp", "SecurityStamp", "LastLoginAt", "NormalizedUserName", "NormalizedEmail",
            "NormalizedName", "AccessFailedCount", "xmin", "UpdatedAt", "PasswordHash",
            "WhatsAppStatus", "WhatsAppError", "WhatsAppAt"
        ];

        /// <summary>Recorded as changed, never with their value.</summary>
        private static readonly HashSet<string> MaskedProperties = ["PasswordHash"];

        private static readonly string[] LabelProperties = ["InventoryCode", "Model", "Name", "UserName", "RequestType"];

        private readonly AuditContext _audit;
        private readonly DbSession _session;
        private readonly IServiceProvider _services;
        private List<Pending>? _pending;
        private bool _ownTransaction;

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
            Capture(eventData.Context);

            // Without a request-wide transaction EF would commit the change before its audit rows exist.
            if (_pending is { Count: > 0 } && !_session.InTransaction && eventData.Context != null)
            {
                await _session.BeginAsync(cancellationToken);
                _session.Enlist(eventData.Context);
                _ownTransaction = true;
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
                if (_ownTransaction)
                {
                    _ownTransaction = false;
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

        private async Task RollbackOwnTransactionAsync()
        {
            if (!_ownTransaction) return;
            _ownTransaction = false;
            await _session.RollbackAsync();
        }

        private void Capture(DbContext? context)
        {
            _pending = null;
            if (context == null) return;

            foreach (var entry in context.ChangeTracker.Entries())
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
                        operation = AuditOperations.Updated;
                        fields = Fields(entry, "", (p, _) => (p.OriginalValue, p.CurrentValue), includeUnchanged: false);
                        // Identity marks every column on Update(). A sign-in that only stamped LastLoginAt is not an action.
                        if (fields.Count == 0) continue;
                        break;
                    default:
                        continue;
                }

                // Deleted rows are labelled now. Others wait until after the save, when generated ids exist.
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

        /// <summary>The entry's columns and those of its owned parts, without keys. For an update only real changes.</summary>
        private static List<AuditFieldChange> Fields(
            EntityEntry entry, string prefix,
            Func<PropertyEntry, EntityEntry, (object? Old, object? New)> values, bool includeUnchanged)
        {
            var result = new List<AuditFieldChange>();
            foreach (var property in entry.Properties)
            {
                var name = property.Metadata.Name;
                if (property.Metadata.IsPrimaryKey() || property.Metadata.IsShadowProperty() && property.Metadata.IsForeignKey())
                    continue;
                if (IgnoredProperties.Contains(name) && !MaskedProperties.Contains(name))
                    continue;
                if (!includeUnchanged && !property.IsModified)
                    continue;

                var (oldValue, newValue) = values(property, entry);
                var oldText = Format(oldValue);
                var newText = Format(newValue);
                if (oldText == newText || (includeUnchanged && oldText == null && newText == null))
                    continue;

                if (MaskedProperties.Contains(name))
                    result.Add(new AuditFieldChange(prefix + name, null, "(changed)"));
                else
                    result.Add(new AuditFieldChange(prefix + name, oldText, newText));
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
            foreach (var reference in entry.References)
            {
                if (reference.TargetEntry is { } target)
                    From(target);   // Owned parts and loaded parents
            }
            return parts.Count == 0 ? null : Truncate(string.Join(" · ", parts.Take(3)));
        }

        private static string? Format(object? value)
        {
            var text = value switch
            {
                null => null,
                string s => s,
                DateTime d when d == DateTime.MinValue => null,   // Means not set yet
                DateTime d => d.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture),
                DateTimeOffset d => d.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture),
                bool b => b ? "true" : "false",
                System.Enum e => e.ToString(),
                IEnumerable list => JsonSerializer.Serialize(list),
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            };
            return text == null ? null : Truncate(text);
        }

        private static string Truncate(string text)
            => text.Length <= MaxValueLength ? text : text[..MaxValueLength] + "…";

        private sealed record Pending(EntityEntry Entry, string Operation, List<AuditFieldChange> Fields, string? Label);
    }
}
