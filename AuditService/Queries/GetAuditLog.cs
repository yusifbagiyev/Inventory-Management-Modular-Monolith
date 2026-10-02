using System.Text.Json;
using AuditService.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SharedServices.Auditing;

namespace AuditService.Queries
{
    /// <summary>One action, meaning the rows one request wrote.</summary>
    public sealed record AuditActionDto(
        string CorrelationId,
        DateTime At,
        int? UserId,
        string? UserName,
        string? IpAddress,
        string Action,
        IReadOnlyList<AuditEntryDto> Entries);

    public sealed record AuditEntryDto(
        long Id,
        DateTime At,
        string EntityType,
        string? EntityId,
        string? Label,
        string Operation,
        IReadOnlyList<AuditFieldChange> Changes);

    public sealed record AuditLogPage(IReadOnlyList<AuditActionDto> Items, int TotalCount, int PageNumber, int PageSize);

    public sealed record AuditUserOption(int? UserId, string UserName);

    public sealed record AuditFacets(IReadOnlyList<AuditUserOption> Users, IReadOnlyList<string> EntityTypes);

    /// <summary>The audit log paged by action, where filters apply to rows so an action lists only its matches.</summary>
    public sealed record GetAuditLogQuery(
        int PageNumber = 1,
        int PageSize = 30,
        string? Search = null,
        int? UserId = null,
        string? EntityType = null,
        string? Operation = null,
        DateTime? From = null,
        DateTime? To = null,
        string? EntityId = null) : IRequest<AuditLogPage>;

    public sealed record GetAuditFacetsQuery : IRequest<AuditFacets>;

    public sealed class GetAuditLogHandler :
        IRequestHandler<GetAuditLogQuery, AuditLogPage>,
        IRequestHandler<GetAuditFacetsQuery, AuditFacets>
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private readonly AuditDbContext _context;

        public GetAuditLogHandler(AuditDbContext context) => _context = context;

        public async Task<AuditLogPage> Handle(GetAuditLogQuery request, CancellationToken cancellationToken)
        {
            var pageSize = Math.Clamp(request.PageSize, 1, 200);
            var pageNumber = Math.Max(1, request.PageNumber);

            var rows = _context.Entries.AsNoTracking();
            if (request.UserId.HasValue)
                rows = rows.Where(e => e.UserId == request.UserId);
            if (!string.IsNullOrWhiteSpace(request.EntityType))
                rows = rows.Where(e => e.EntityType == request.EntityType);
            if (!string.IsNullOrWhiteSpace(request.EntityId))
                rows = rows.Where(e => e.EntityId == request.EntityId);
            if (!string.IsNullOrWhiteSpace(request.Operation))
                rows = rows.Where(e => e.Operation == request.Operation);
            if (request.From.HasValue)
                rows = rows.Where(e => e.At >= request.From.Value);
            if (request.To.HasValue)
                rows = rows.Where(e => e.At <= request.To.Value);
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                // Escape the LIKE wildcards so the search matches the text literally
                var pattern = "%" + request.Search.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";
                rows = rows.Where(e =>
                    EF.Functions.ILike(e.Label ?? "", pattern) ||
                    EF.Functions.ILike(e.UserName ?? "", pattern) ||
                    EF.Functions.ILike(e.Action, pattern) ||
                    EF.Functions.ILike(e.EntityId ?? "", pattern) ||
                    EF.Functions.ILike(e.Changes, pattern));
            }

            var total = await rows.Select(e => e.CorrelationId).Distinct().CountAsync(cancellationToken);
            var page = await rows
                .GroupBy(e => e.CorrelationId)
                .Select(g => new { CorrelationId = g.Key, At = g.Max(e => e.At) })
                .OrderByDescending(g => g.At)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var ids = page.Select(p => p.CorrelationId).ToList();
            var entries = await rows
                .Where(e => ids.Contains(e.CorrelationId))
                .OrderBy(e => e.Id)
                .ToListAsync(cancellationToken);
            var byAction = entries.ToLookup(e => e.CorrelationId);

            var items = page.Select(p =>
            {
                var group = byAction[p.CorrelationId].ToList();
                var first = group[0];
                return new AuditActionDto(
                    p.CorrelationId, p.At, first.UserId, first.UserName, first.IpAddress, first.Action,
                    group.Select(e => new AuditEntryDto(
                        e.Id, e.At, e.EntityType, e.EntityId, e.Label, e.Operation,
                        JsonSerializer.Deserialize<List<AuditFieldChange>>(e.Changes, Json) ?? [])).ToList());
            }).ToList();

            return new AuditLogPage(items, total, pageNumber, pageSize);
        }

        public async Task<AuditFacets> Handle(GetAuditFacetsQuery request, CancellationToken cancellationToken)
        {
            var users = await _context.Entries.AsNoTracking()
                .Where(e => e.UserName != null)
                .GroupBy(e => e.UserId)
                .Select(g => new { UserId = g.Key, UserName = g.Max(e => e.UserName)! })
                .ToListAsync(cancellationToken);
            var types = await _context.Entries.AsNoTracking()
                .Select(e => e.EntityType).Distinct().OrderBy(t => t)
                .ToListAsync(cancellationToken);
            return new AuditFacets(
                users.Select(u => new AuditUserOption(u.UserId, u.UserName)).OrderBy(u => u.UserName).ToList(),
                types);
        }
    }
}
