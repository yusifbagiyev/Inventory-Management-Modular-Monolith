using InventoryManagement.Web.Extensions;
using SharedServices.Identity;
using InventoryManagement.Web.Filters;
using System.Diagnostics;
using InventoryManagement.Web.Localization;
using InventoryManagement.Web.Models;
using InventoryManagement.Web.Models.ViewModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ProductService.Application.Features.Products.Queries;
using RouteService.Application.Features.Routes.Queries;
using RouteService.Domain.Common;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
    public class HomeController : BaseController
    {
        private readonly IMediator _mediator;

        public HomeController(IMediator mediator, ILogger<HomeController> logger)
            : base(logger)
        {
            _mediator = mediator;
        }

        /// <summary>Sends the user to the first page they may open, else to Notifications which everyone can open.</summary>
        public IActionResult Index()
        {
            var pages = new (string Permission, string Url)[]
            {
                (AllPermissions.DashboardView, "/Home/Dashboard"),
                (AllPermissions.ProductView, "/Products"),
                (AllPermissions.RouteView, "/Routes"),
                (AllPermissions.ApprovalView, "/Approvals"),
                (AllPermissions.ApprovalDecide, "/Approvals"),
                (AllPermissions.CategoryView, "/Categories"),
                (AllPermissions.DepartmentView, "/Departments"),
                (AllPermissions.UserView, "/UserManagement"),
                (AllPermissions.AuditView, "/Audit"),
            };
            return Redirect(pages.FirstOrDefault(p => User.HasPermission(p.Permission)).Url ?? "/Notifications");
        }

        /// <summary>Period figures come from transfers created in the period, while product figures show the current state.</summary>
        [PermissionAuthorize(AllPermissions.DashboardView)]
        public async Task<IActionResult> Dashboard(string period = "last7days")
        {
            period = period.ToLowerInvariant();
            var now = DateTime.Now;
            var endDate = now.Date.AddDays(1).AddTicks(-1);
            var startDate = period switch
            {
                "last30days" => now.Date.AddDays(-29),
                "last90days" => now.Date.AddDays(-89),
                "all" => DateTime.MinValue,
                _ => now.Date.AddDays(-6)
            };
            if (period is not ("last30days" or "last90days" or "all"))
                period = "last7days";

            var transfers = await _mediator.Send(new GetTransferActivityQuery(startDate, endDate));
            // Product tiles ignore the period and show the current state
            var products = await _mediator.Send(new GetProductCountsQuery());
            var faulty = products.NotWorking > 0
                ? (await _mediator.Send(new GetAllProductsQuery(1, 2, status: false))).Items
                    .Select(p => string.IsNullOrWhiteSpace(p.Model) ? $"#{p.InventoryCode}" : p.Model!).ToList()
                : [];
            var pending = await _mediator.Send(new GetAllRoutesQuery(1, 1000, IsCompleted: false));
            var oldestPending = pending.Items.Select(r => (DateTime?)r.CreatedAt).Min();

            var categories = BuildCategoryDistribution(transfers);
            var model = new DashboardViewModel
            {
                TotalProducts = products.Total,
                ActiveProducts = products.Active,
                NotWorking = products.NotWorking,
                NotWorkingNames = faulty,
                CompletedTransfers = transfers.Count(t => t.IsCompleted),
                PendingTransfers = pending.TotalCount,
                OldestPendingDays = oldestPending is { } oldest ? (int)(now.Date - oldest.Date).TotalDays : null,
                CategoryDistributions = categories,
                TotalTransfers = transfers.Count,
                // Same row count as categories so the two lists side by side end level
                DepartmentStats = BuildDepartmentStats(transfers, categories.Count),
                TransferActivityData = BuildTransferActivity(transfers, startDate, endDate, period),
                PeriodStart = period == "all" ? null : startDate.ToString("yyyy-MM-dd"),
                PeriodEnd = period == "all" ? null : endDate.ToString("yyyy-MM-dd")
            };

            ViewBag.CurrentPeriod = period;

            // The needs-attention block also ignores the period
            ViewBag.NotWorking = products.NotWorking;
            ViewBag.OpenTransfers = pending.TotalCount;
            if (User.HasPermission(AllPermissions.ApprovalView))
                ViewBag.PendingApprovals = (await _mediator.Send(new ApprovalService.Application.Features.Queries.GetApprovalStatistics.Query())).Pending;

            return View(model);
        }

        /// <summary>Busiest departments in the period, grouped by the name stored on each transfer.</summary>
        private static List<DepartmentStats> BuildDepartmentStats(IReadOnlyList<TransferActivity> transfers, int count)
        {
            var byDepartment = new Dictionary<string, DepartmentTally>(StringComparer.Ordinal);

            static string Key(int id, string? name) => string.IsNullOrWhiteSpace(name) ? $"#{id}" : name;

            void Add(string key, int productId, string? worker, bool countTransfer)
            {
                if (!byDepartment.TryGetValue(key, out var tally))
                    byDepartment[key] = tally = new DepartmentTally();
                if (countTransfer)
                    tally.Transfers++;
                tally.Products.Add(productId);
                if (!string.IsNullOrWhiteSpace(worker))
                    tally.Workers.Add(worker.Trim());
            }

            foreach (var t in transfers)
            {
                var to = Key(t.ToDepartmentId, t.ToDepartmentName);
                Add(to, t.ProductId, t.ToWorker, countTransfer: true);
                if (t.FromDepartmentId is { } fromId)
                {
                    var from = Key(fromId, t.FromDepartmentName);
                    // A transfer that stays inside one department is one transfer for it, not two
                    Add(from, t.ProductId, t.FromWorker, countTransfer: from != to);
                }
            }

            return byDepartment
                .Select(d => new DepartmentStats
                {
                    DepartmentName = d.Key,
                    ProductCount = d.Value.Products.Count,
                    ActiveWorkers = d.Value.Workers.Count,
                    PeriodTransfers = d.Value.Transfers
                })
                .OrderByDescending(d => d.PeriodTransfers)
                .ThenByDescending(d => d.ProductCount)
                .Take(count)
                .ToList();
        }

        /// <summary>Running figures of one department, so no list of its transfers has to be kept or searched.</summary>
        private sealed class DepartmentTally
        {
            public int Transfers { get; set; }
            public HashSet<int> Products { get; } = [];
            public HashSet<string> Workers { get; } = new(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Transfers per category the product had when it was transferred, the smallest beyond eight joined as one row.</summary>
        private static List<CategoryDistribution> BuildCategoryDistribution(IReadOnlyList<TransferActivity> transfers)
        {
            const int rows = 8;
            var all = transfers
                .GroupBy(t => t.CategoryName)
                .Select(g => new CategoryDistribution { CategoryName = g.Key, Count = g.Count() })
                .OrderByDescending(c => c.Count)
                .ThenBy(c => c.CategoryName, StringComparer.Ordinal)
                .ToList();
            if (all.Count <= rows)
                return all;

            var rest = all.Skip(rows - 1).ToList();
            return [.. all.Take(rows - 1), new CategoryDistribution { IsOther = true, OtherCategories = rest.Count, Count = rest.Sum(c => c.Count) }];
        }

        /// <summary>Bucket size grows with the period, from days up to quarters.</summary>
        private static TransferActivityData BuildTransferActivity(
            IReadOnlyList<TransferActivity> transfers, DateTime startDate, DateTime endDate, string period)
        {
            var data = new TransferActivityData();
            if (transfers.Count == 0)
                return data;

            void AddBucket(string label, DateTime from, DateTime toExclusive)
            {
                var bucket = transfers.Where(t => t.CreatedAt.Date >= from && t.CreatedAt.Date < toExclusive).ToList();
                data.Labels.Add(label);
                data.CompletedData.Add(bucket.Count(t => t.IsCompleted));
                data.PendingData.Add(bucket.Count(t => !t.IsCompleted));
                data.BucketStarts.Add(from.ToString("yyyy-MM-dd"));
                data.BucketEnds.Add(toExclusive.AddDays(-1).ToString("yyyy-MM-dd"));
            }

            switch (period)
            {
                case "last30days":
                case "last90days":
                    // Weekly buckets labelled by their first and last day, the last one cut at today
                    for (var from = startDate.Date; from <= endDate; from = from.AddDays(7))
                    {
                        var toExclusive = from.AddDays(7) > endDate ? endDate.Date.AddDays(1) : from.AddDays(7);
                        AddBucket(RangeName(from, toExclusive.AddDays(-1)), from, toExclusive);
                    }
                    break;

                case "all":
                    var first = transfers.Min(t => t.CreatedAt);
                    for (var quarter = new DateTime(first.Year, ((first.Month - 1) / 3) * 3 + 1, 1); quarter <= endDate; quarter = quarter.AddMonths(3))
                        AddBucket(QuarterName(quarter), quarter, quarter.AddMonths(3));
                    break;

                default:
                    for (var i = 6; i >= 0; i--)
                    {
                        var day = DateTime.Now.Date.AddDays(-i);
                        AddBucket(DayName(day), day, day.AddDays(1));
                    }
                    break;
            }

            return data;
        }

        // Hand-written because the formatting culture stays en-US
        private static readonly string[] AzDays = ["B.", "B.e.", "Ç.a.", "Ç.", "C.a.", "C.", "Ş."];
        private static readonly string[] RuDays = ["Вс", "Пн", "Вт", "Ср", "Чт", "Пт", "Сб"];
        private static readonly string[] Roman = ["I", "II", "III", "IV"];

        private static string DayName(DateTime date)
            => JsonStringLocalizer.IsAzerbaijani ? $"{AzDays[(int)date.DayOfWeek]} {date:dd}.{date:MM}"
             : JsonStringLocalizer.IsRussian ? $"{RuDays[(int)date.DayOfWeek]} {date:dd}.{date:MM}"
             : $"{date:ddd} {date:dd}.{date:MM}";

        private static string RangeName(DateTime first, DateTime last)
            => first.Month == last.Month ? $"{first:dd}–{last:dd}.{last:MM}" : $"{first:dd}.{first:MM}–{last:dd}.{last:MM}";

        private static string QuarterName(DateTime quarterStart)
        {
            var q = (quarterStart.Month - 1) / 3;
            return JsonStringLocalizer.IsAzerbaijani ? $"{Roman[q]} rüb {quarterStart.Year}"
                 : JsonStringLocalizer.IsRussian ? $"{Roman[q]} кв. {quarterStart.Year}"
                 : $"Q{q + 1} {quarterStart.Year}";
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var errorViewModel = new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            };

            if (IsAjaxRequest())
            {
                return Json(new
                {
                    isSuccess = false,
                    message = Tr("An error occurred while processing your request"),
                    requestId = errorViewModel.RequestId
                });
            }

            return View(errorViewModel);
        }

        /// <summary>Explains any error status that came without content, keeping the status that really happened.</summary>
        [AllowAnonymous]
        [Route("NotFound")]
        [ActionName("NotFound")]
        public IActionResult StatusPage()
        {
            // Only a re-executed request carries a real status, so this address typed with any code is just not found
            var statusCode = HttpContext.Features.Get<IStatusCodeReExecuteFeature>()?.OriginalStatusCode
                             ?? StatusCodes.Status404NotFound;

            // A failed image or script request needs the status alone, which also spares the layout's queries
            if (!Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase))
                return StatusCode(statusCode);

            Response.StatusCode = statusCode;
            return View("Status", new StatusPageViewModel
            {
                StatusCode = statusCode,
                // Only a stale form token gives a page request a 400, and opening the form's page again issues a new one
                ReloadUrl = statusCode == StatusCodes.Status400BadRequest ? LocalReferer() : null
            });
        }

        /// <summary>The page of this site the request was sent from, as a local address.</summary>
        private string? LocalReferer()
        {
            if (!Uri.TryCreate(Request.Headers.Referer.ToString(), UriKind.Absolute, out var referer)
                || !string.Equals(referer.Authority, Request.Host.Value, StringComparison.OrdinalIgnoreCase))
                return null;

            return Url.IsLocalUrl(referer.PathAndQuery) ? referer.PathAndQuery : null;
        }
    }
}
