using System.Text.RegularExpressions;
using ApprovalService.Domain.Entities;
using ApprovalService.Domain.Enums;
using ApprovalService.Domain.Repositories;
using ApprovalService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SharedServices.Contracts;

namespace ApprovalService.Infrastructure.Repositories
{
    public class ApprovalRequestRepository : IApprovalRequestRepository
    {
        private const int MaxPageSize = 200;

        // ActionData up to this length is read as stored, longer values usually carry uploaded photos
        private const int InlineLength = 8000;

        private static readonly TimeSpan WithoutImagesLifetime = TimeSpan.FromHours(1);

        private readonly ApprovalDbContext _context;
        private readonly IMemoryCache _cache;

        public ApprovalRequestRepository(ApprovalDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<ApprovalRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.ApprovalRequests.FindAsync(new object[] { id }, cancellationToken);
        }


        public async Task<ApprovalRequestSummary?> GetSummaryByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return (await SummariesAsync(_context.ApprovalRequests.Where(r => r.Id == id), cancellationToken)).FirstOrDefault();
        }


        public Task<IReadOnlyList<ApprovalRequestSummary>> GetPendingAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            return SummariesAsync(Page(_context.ApprovalRequests
                    .Where(r => r.Status == ApprovalStatus.Pending)
                    .OrderByDescending(r => r.CreatedAt), pageNumber, pageSize),
                cancellationToken);
        }


        public Task<IReadOnlyList<ApprovalRequestSummary>> GetPendingAsync(IReadOnlyCollection<string> requestTypes, int take, CancellationToken cancellationToken = default)
        {
            return SummariesAsync(_context.ApprovalRequests
                    .Where(r => r.Status == ApprovalStatus.Pending && requestTypes.Contains(r.RequestType))
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(take),
                cancellationToken);
        }


        public async Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
        {
            return await _context.ApprovalRequests
                .CountAsync(r => r.Status == ApprovalStatus.Pending, cancellationToken);
        }


        public Task<IReadOnlyList<ApprovalRequestSummary>> GetDecidedAsync(IReadOnlyCollection<ApprovalStatus> statuses, int take, CancellationToken cancellationToken = default)
        {
            return SummariesAsync(_context.ApprovalRequests
                    .Where(r => statuses.Contains(r.Status))
                    .OrderByDescending(r => r.ProcessedAt ?? r.CreatedAt)
                    .Take(take),
                cancellationToken);
        }


        public Task<IReadOnlyList<ApprovalRequestSummary>> GetByUserIdAsync(int userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            return SummariesAsync(Page(_context.ApprovalRequests
                    .Where(r => r.RequestedById == userId)
                    .OrderByDescending(r => r.CreatedAt), pageNumber, pageSize),
                cancellationToken);
        }


        public async Task<IReadOnlyDictionary<ApprovalStatus, int>> CountByStatusAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _context.ApprovalRequests
                .Where(r => r.RequestedById == userId)
                .GroupBy(r => r.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Status, g => g.Count, cancellationToken);
        }


        public Task<int> CountAsync(ApprovalStatus status, DateTime? processedSince = null, CancellationToken cancellationToken = default)
        {
            var query = _context.ApprovalRequests.Where(r => r.Status == status);
            if (processedSince.HasValue)
                query = query.Where(r => r.ProcessedAt >= processedSince.Value);
            return query.CountAsync(cancellationToken);
        }


        public async Task<ApprovalRequest> AddAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
        {
            await _context.ApprovalRequests.AddAsync(request, cancellationToken);
            return request;
        }


        public Task UpdateAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
        {
            _context.Entry(request).State = EntityState.Modified;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ApprovalRequestSummary>> GetAllAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            return SummariesAsync(Page(_context.ApprovalRequests
                    .OrderByDescending(r => r.CreatedAt), pageNumber, pageSize),
                cancellationToken);
        }

        public Task<int> CountAllAsync(CancellationToken cancellationToken = default)
        {
            return _context.ApprovalRequests.CountAsync(cancellationToken);
        }

        public Task DeleteAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
        {
            _context.ApprovalRequests.Remove(request);
            return Task.CompletedTask;
        }

        public async Task<bool> DropImageDataAsync(int id, CancellationToken cancellationToken = default)
        {
            // Run in the database so the photos are never read, and outside change tracking since it is housekeeping, not an action
            var changed = await _context.ApprovalRequests
                .Where(r => r.Id == id && r.Status != ApprovalStatus.Pending
                            && Regex.IsMatch(r.ActionData, ApprovalActionData.ImageBytesPattern))
                .ExecuteUpdateAsync(update => update.SetProperty(
                        r => r.ActionData,
                        r => ApprovalDbContext.RegexpReplace(r.ActionData, ApprovalActionData.ImageBytesPattern, ApprovalActionData.ImageBytesReplacement, "g")),
                    cancellationToken);
            return changed > 0;
        }

        public async Task<IReadOnlyList<int>> GetLargeDecidedIdsAsync(int afterId, int take, CancellationToken cancellationToken = default)
        {
            return await _context.ApprovalRequests
                .Where(r => r.Id > afterId && r.Status != ApprovalStatus.Pending
                            && r.ActionData.Substring(0, InlineLength + 1).Length > InlineLength)
                .OrderBy(r => r.Id)
                .Select(r => r.Id)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        private static IQueryable<ApprovalRequest> Page(IQueryable<ApprovalRequest> query, int pageNumber, int pageSize)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
            return query.Skip((pageNumber - 1) * pageSize).Take(pageSize);
        }

        /// <summary>Reads the rows without ever loading uploaded photos, which stay in the database.</summary>
        private async Task<IReadOnlyList<ApprovalRequestSummary>> SummariesAsync(IQueryable<ApprovalRequest> query, CancellationToken cancellationToken)
        {
            var rows = await query.AsNoTracking()
                .Select(r => new ApprovalRequestSummary
                {
                    Id = r.Id,
                    RequestType = r.RequestType,
                    EntityType = r.EntityType,
                    EntityId = r.EntityId,
                    // One character more than the limit tells a longer value from one that fits
                    ActionData = r.ActionData.Substring(0, InlineLength + 1),
                    RequestedById = r.RequestedById,
                    RequestedByName = r.RequestedByName,
                    ApprovedById = r.ApprovedById,
                    ApprovedByName = r.ApprovedByName,
                    Status = r.Status,
                    RejectionReason = r.RejectionReason,
                    CreatedAt = r.CreatedAt,
                    ProcessedAt = r.ProcessedAt,
                    ExecutedAt = r.ExecutedAt
                })
                .ToListAsync(cancellationToken);

            var cut = rows.Where(r => r.ActionData.Length > InlineLength).Select(r => r.Id).ToList();
            var withoutImages = cut.Count > 0
                ? await ActionDataWithoutImagesAsync(cut, cancellationToken)
                : new Dictionary<int, string>();

            // A value that fits can still hold a small image, so every row leaves here without image bytes
            return rows
                .Select(r => r with
                {
                    ActionData = r.ActionData.Length > InlineLength
                        ? withoutImages.GetValueOrDefault(r.Id, "{}")
                        : ApprovalActionData.WithoutImageBytes(r.ActionData)
                })
                .ToList();
        }

        private async Task<Dictionary<int, string>> ActionDataWithoutImagesAsync(List<int> ids, CancellationToken cancellationToken)
        {
            var result = new Dictionary<int, string>();
            var missing = new List<int>();
            foreach (var id in ids)
            {
                if (_cache.TryGetValue(CacheKey(id), out string? cached) && cached != null)
                    result[id] = cached;
                else
                    missing.Add(id);
            }

            if (missing.Count > 0)
            {
                var read = await _context.ApprovalRequests.AsNoTracking()
                    .Where(r => missing.Contains(r.Id))
                    .Select(r => new
                    {
                        r.Id,
                        Data = ApprovalDbContext.RegexpReplace(r.ActionData, ApprovalActionData.ImageBytesPattern, ApprovalActionData.ImageBytesReplacement, "g")
                    })
                    .ToListAsync(cancellationToken);

                foreach (var row in read)
                {
                    result[row.Id] = row.Data;
                    // Never stale, as ActionData only ever changes to this same text when the photos are dropped
                    _cache.Set(CacheKey(row.Id), row.Data, new MemoryCacheEntryOptions { SlidingExpiration = WithoutImagesLifetime });
                }
            }
            return result;
        }

        private static string CacheKey(int id) => $"approval:action-data:{id}";
    }
}
