using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SharedServices.Auditing;

namespace AuditService.Data
{
    /// <summary>Saves audit records on the request's connection, inside its transaction.</summary>
    public sealed class AuditSink : IAuditSink
    {
        // Letters and signs like & are stored as they are rather than escaped, so the log can be searched for them
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        private readonly IServiceProvider _services;

        // Resolved lazily because module DbContexts ask for the sink while their options are built
        public AuditSink(IServiceProvider services) => _services = services;

        public async Task WriteAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default)
        {
            var context = _services.GetRequiredService<AuditDbContext>();
            context.Entries.AddRange(records.Select(ToEntry));
            await context.SaveChangesAsync(cancellationToken);
            context.ChangeTracker.Clear();
        }

        public void Write(IReadOnlyList<AuditRecord> records)
        {
            var context = _services.GetRequiredService<AuditDbContext>();
            context.Entries.AddRange(records.Select(ToEntry));
            context.SaveChanges();
            context.ChangeTracker.Clear();
        }

        private static AuditEntry ToEntry(AuditRecord r) => new()
        {
            At = r.At,
            CorrelationId = r.CorrelationId,
            UserId = r.UserId,
            UserName = r.UserName,
            IpAddress = r.IpAddress,
            Action = r.Action,
            EntityType = r.EntityType,
            EntityId = r.EntityId,
            Label = r.Label,
            Operation = r.Operation,
            Changes = JsonSerializer.Serialize(r.Changes, Json)
        };
    }
}
