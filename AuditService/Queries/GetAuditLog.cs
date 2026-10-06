using SharedServices.Persistence;
using System.Text.Json;
using AuditService.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SharedServices.Auditing;

namespace AuditService.Queries
{
    /// <summary>One action, meaning the rows one request wrote.</summary>
    public sealed record AuditActionDto(
        DateTime At,
        string? UserName,
        string? IpAddress,
        string Action,
        IReadOnlyList<AuditEntryDto> Entries);

    public sealed record AuditEntryDto(
        string EntityType,
        string? EntityId,
        string? Label,
        string Operation,
        IReadOnlyList<AuditFieldChange> Changes);

    public sealed record AuditLogPage(IReadOnlyList<AuditActionDto> Items, int TotalCount, int PageNumber, int PageSize);

    public sealed record AuditUserOption(int? UserId, string UserName);

    public sealed record AuditFacets(IReadOnlyList<AuditUserOption> Users, IReadOnlyList<string> EntityTypes);

    /// <summary>The audit log paged by action, where filters apply to rows and an empty list means no filter.</summary>
    public sealed record GetAuditLogQuery(
        int PageNumber = 1,
        int PageSize = 30,
        string? Search = null,
        IReadOnlyList<int>? UserIds = null,
        IReadOnlyList<string>? EntityTypes = null,
        IReadOnlyList<string>? Operations = null,
        DateTime? From = null,
        DateTime? To = null,
        string? EntityId = null,
        string? Sort = null,
        bool Descending = true) : IRequest<AuditLogPage>;

    public sealed record GetAuditFacetsQuery : IRequest<AuditFacets>;

    public sealed class GetAuditLogHandler :
        IRequestHandler<GetAuditLogQuery, AuditLogPage>,
        IRequestHandler<GetAuditFacetsQuery, AuditFacets>
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private const string FacetsKey = "audit:facets";
        private static readonly TimeSpan FacetsLifetime = TimeSpan.FromMinutes(5);
        private readonly AuditDbContext _context;
        private readonly IMemoryCache _cache;

        public GetAuditLogHandler(AuditDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<AuditLogPage> Handle(GetAuditLogQuery request, CancellationToken cancellationToken)
        {
            var pageSize = Math.Clamp(request.PageSize, 1, 200);
            var pageNumber = Math.Max(1, request.PageNumber);

            var rows = _context.Entries.AsNoTracking();
            var userIds = request.UserIds?.Distinct().ToList() ?? [];
            if (userIds.Count > 0)
                rows = rows.Where(e => e.UserId.HasValue && userIds.Contains(e.UserId.Value));
            var entityTypes = Values(request.EntityTypes);
            if (entityTypes.Count > 0)
                rows = rows.Where(e => entityTypes.Contains(e.EntityType));
            if (!string.IsNullOrWhiteSpace(request.EntityId))
                rows = rows.Where(e => e.EntityId == request.EntityId);
            var operations = Values(request.Operations);
            if (operations.Count > 0)
                rows = rows.Where(e => operations.Contains(e.Operation));
            if (request.From.HasValue)
                rows = rows.Where(e => e.At >= request.From.Value);
            if (request.To.HasValue)
                rows = rows.Where(e => e.At <= request.To.Value);
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var pattern = SearchSql.Contains(request.Search);
                rows = rows.Where(e =>
                    EF.Functions.ILike(SearchSql.Fold(e.Label ?? ""), pattern) ||
                    EF.Functions.ILike(SearchSql.Fold(e.UserName ?? ""), pattern) ||
                    EF.Functions.ILike(e.Action, pattern) ||
                    EF.Functions.ILike(e.EntityId ?? "", pattern) ||
                    EF.Functions.ILike(SearchSql.Fold(e.Changes), pattern));
            }

            // The newest matching row stands for its action, so the newest page is read from the date index instead of grouping the whole log
            var actions = rows.Where(e => !rows.Any(o => o.CorrelationId == e.CorrelationId && o.Id > e.Id));
            var desc = request.Descending;
            // The row id breaks ties, so paging never repeats or skips an action
            var ordered = request.Sort switch
            {
                "user" => (desc ? actions.OrderByDescending(e => e.UserName) : actions.OrderBy(e => e.UserName))
                    .ThenByDescending(e => e.At).ThenByDescending(e => e.Id),
                "action" => (desc ? actions.OrderByDescending(e => e.Action) : actions.OrderBy(e => e.Action))
                    .ThenByDescending(e => e.At).ThenByDescending(e => e.Id),
                _ => desc
                    ? actions.OrderByDescending(e => e.At).ThenByDescending(e => e.Id)
                    : actions.OrderBy(e => e.At).ThenBy(e => e.Id)
            };
            var skipped = (pageNumber - 1) * pageSize;
            var page = await ordered
                .Skip(skipped)
                .Take(pageSize)
                .Select(e => new { e.CorrelationId, e.At })
                .ToListAsync(cancellationToken);

            // A page that is not full is the last one, so the total is known without another pass over the log
            var isLastPage = page.Count < pageSize && (page.Count > 0 || pageNumber == 1);
            var total = isLastPage
                ? skipped + page.Count
                : await rows.Select(e => e.CorrelationId).Distinct().CountAsync(cancellationToken);

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
                    p.At, first.UserName, first.IpAddress, first.Action,
                    group.Select(e => new AuditEntryDto(
                        e.EntityType, e.EntityId, e.Label, e.Operation,
                        JsonSerializer.Deserialize<List<AuditFieldChange>>(e.Changes, Json) ?? [])).ToList());
            }).ToList();

            return new AuditLogPage(items, total, pageNumber, pageSize);
        }

        static List<string> Values(IReadOnlyList<string>? values)
            => values?.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToList() ?? [];

        // Cached because both lists read the whole log on every view, yet change only when a new user or kind of record first appears
        public async Task<AuditFacets> Handle(GetAuditFacetsQuery request, CancellationToken cancellationToken)
            => (await _cache.GetOrCreateAsync(FacetsKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = FacetsLifetime;
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
            }))!;
    }
}
