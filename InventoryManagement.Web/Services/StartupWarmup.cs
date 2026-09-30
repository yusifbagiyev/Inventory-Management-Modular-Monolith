using System.Diagnostics;
using ApprovalService.Application.Features.Queries;
using MediatR;
using ProductService.Application.Features.Lookups;
using ProductService.Application.Features.Products.Queries;
using RouteService.Application.Features.Routes.Queries;

namespace InventoryManagement.Web.Services
{
    /// <summary>
    /// Runs the queries behind the main pages once after startup. The first execution of each EF
    /// query (translation, JIT) is slow - about 3 s for the dashboard - so without this the first
    /// user after every deploy waited for it.
    /// </summary>
    public sealed class StartupWarmup : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<StartupWarmup> _logger;

        public StartupWarmup(IServiceScopeFactory scopes, ILogger<StartupWarmup> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var watch = Stopwatch.StartNew();
            var today = DateTime.Now.Date;
            var queries = new List<object>
            {
                // Dashboard
                new GetTransferActivityQuery(today.AddDays(-6), today.AddDays(1).AddTicks(-1)),
                new GetProductCountsQuery(),
                new GetProductCountsQuery(today.AddDays(-6), today.AddDays(1).AddTicks(-1)),
                new GetCategoryStatsQuery(),
                new GetDepartmentStatsQuery(),
                // Lists and their filters
                new GetAllProductsQuery(),
                new GetProductFilterFacetsQuery(),
                new GetAllRoutesQuery(),
                new GetLookupsQuery(),
                new GetPendingRequests.Query(1, 10)
            };

            foreach (var query in queries)
            {
                if (stoppingToken.IsCancellationRequested) return;
                try
                {
                    await using var scope = _scopes.CreateAsyncScope();   // DbSession only disposes asynchronously
                    await scope.ServiceProvider.GetRequiredService<IMediator>().Send(query, stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    // Only a head start: a failure here changes nothing for real requests.
                    _logger.LogWarning(ex, "Warm-up query {Query} failed", query.GetType().Name);
                }
            }

            _logger.LogInformation("Warm-up finished in {Elapsed} ms", watch.ElapsedMilliseconds);
        }
    }
}
