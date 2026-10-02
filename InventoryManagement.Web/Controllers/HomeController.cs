using InventoryManagement.Web.Extensions;
using SharedServices.Identity;
using InventoryManagement.Web.Filters;
using System.Diagnostics;
using InventoryManagement.Web.Localization;
using InventoryManagement.Web.Models;
using InventoryManagement.Web.Models.ViewModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductService.Application.Features.Lookups;
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

        /// <summary>
        /// The start page ("/", after sign-in): the first page the user may open. Everyone can open
        /// Notifications, so this never ends on Access denied.
        /// </summary>
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

        /// <summary>
        /// Transfer-centred dashboard. Everything period-based is derived from the transfers created
        /// in the period; product counts are "new in period" (or the whole inventory for "all").
        /// Built from aggregate queries - it used to download every product and every route.
        /// </summary>
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
                // The current month and the five before it, so the monthly bars are whole months.
                "last6months" => new DateTime(now.Year, now.Month, 1).AddMonths(-5),
                "all" => DateTime.MinValue,
                _ => now.Date.AddDays(-6)
            };
            if (period is not ("last30days" or "last90days" or "last6months" or "all"))
                period = "last7days";

            var transfers = await _mediator.Send(new GetTransferActivityQuery(startDate, endDate));
            // The product tiles and "needs attention" show the current state, whatever the period.
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
                // As many departments as category rows, so the two lists side by side end level.
                DepartmentStats = BuildDepartmentStats(transfers, categories.Count),
                TransferActivityData = BuildTransferActivity(transfers, startDate, endDate, period),
                PeriodStart = period == "all" ? null : startDate.ToString("yyyy-MM-dd"),
                PeriodEnd = period == "all" ? null : endDate.ToString("yyyy-MM-dd")
            };

            ViewBag.CurrentPeriod = period;

            // "Needs attention": the current state, whatever the period.
            ViewBag.NotWorking = products.NotWorking;
            ViewBag.OpenTransfers = pending.TotalCount;
            if (User.HasPermission(AllPermissions.ApprovalView))
                ViewBag.PendingApprovals = (await _mediator.Send(new ApprovalService.Application.Features.Queries.GetApprovalStatistics.Query())).Pending;

            return View(model);
        }

        /// <summary>
        /// Top 5 departments by transfers sent or received in the period, by the department name
        /// written on each transfer (what it was called then, even if renamed or deleted since).
        /// </summary>
        private static List<DepartmentStats> BuildDepartmentStats(IReadOnlyList<TransferActivity> transfers, int count)
        {
            var byDepartment = new Dictionary<string, (string Name, List<TransferActivity> Transfers, HashSet<string> Workers)>(StringComparer.Ordinal);

            void Add(int id, string? name, TransferActivity transfer, string? worker)
            {
                var key = string.IsNullOrWhiteSpace(name) ? $"#{id}" : name;
                if (!byDepartment.TryGetValue(key, out var entry))
                    byDepartment[key] = entry = (key, new List<TransferActivity>(), new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                if (!entry.Transfers.Contains(transfer))
                    entry.Transfers.Add(transfer);
                if (!string.IsNullOrWhiteSpace(worker))
                    entry.Workers.Add(worker.Trim());
            }

            foreach (var t in transfers)
            {
                Add(t.ToDepartmentId, t.ToDepartmentName, t, t.ToWorker);
                if (t.FromDepartmentId.HasValue)
                    Add(t.FromDepartmentId.Value, t.FromDepartmentName, t, t.FromWorker);
            }

            return byDepartment.Values
                .Select(d => new DepartmentStats
                {
                    DepartmentName = d.Name,
                    ProductCount = d.Transfers.Select(t => t.ProductId).Distinct().Count(),
                    ActiveWorkers = d.Workers.Count,
                    PeriodTransfers = d.Transfers.Count
                })
                .OrderByDescending(d => d.PeriodTransfers)
                .ThenByDescending(d => d.ProductCount)
                .Take(count)
                .ToList();
        }

        /// <summary>Categories of the products transferred in the period (category at transfer time).</summary>
        private static List<CategoryDistribution> BuildCategoryDistribution(IReadOnlyList<TransferActivity> transfers)
            => transfers
                .GroupBy(t => t.CategoryName)
                .Select(g => (Name: g.Key, Count: g.Select(t => t.ProductId).Distinct().Count()))
                .OrderByDescending(c => c.Count)
                .Take(8)
                .Select(c => new CategoryDistribution { CategoryName = c.Name, Count = c.Count })
                .ToList();

        /// <summary>Completed/pending transfer counts per day, week, month or quarter depending on the period.</summary>
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
                    var week = 1;
                    for (var from = startDate; from <= endDate && week <= 10; from = from.AddDays(7), week++)
                        AddBucket(Tr("Week {0}").Replace("{0}", week.ToString()), from.Date, from.AddDays(7).Date);
                    break;

                case "last90days":
                    // Weeks, labelled by their first day.
                    for (var from = startDate; from <= endDate; from = from.AddDays(7))
                        AddBucket(from.ToString("dd.MM"), from.Date, from.AddDays(7).Date);
                    break;

                case "last6months":
                    for (var i = 5; i >= 0; i--)
                    {
                        var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-i);
                        AddBucket($"{MonthName(monthStart)} {monthStart.Year}", monthStart, monthStart.AddMonths(1));
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

        // Chart labels in the interface language (the formatting culture stays en-US).
        private static readonly string[] AzMonths = ["Yan", "Fev", "Mar", "Apr", "May", "İyn", "İyl", "Avq", "Sen", "Okt", "Noy", "Dek"];
        private static readonly string[] AzDays = ["B.", "B.e.", "Ç.a.", "Ç.", "C.a.", "C.", "Ş."];
        private static readonly string[] RuMonths = ["янв", "фев", "мар", "апр", "май", "июн", "июл", "авг", "сен", "окт", "ноя", "дек"];
        private static readonly string[] RuDays = ["Вс", "Пн", "Вт", "Ср", "Чт", "Пт", "Сб"];
        private static readonly string[] Roman = ["I", "II", "III", "IV"];

        private static string MonthName(DateTime date)
            => JsonStringLocalizer.IsAzerbaijani ? AzMonths[date.Month - 1]
             : JsonStringLocalizer.IsRussian ? RuMonths[date.Month - 1]
             : date.ToString("MMM");

        private static string DayName(DateTime date)
            => JsonStringLocalizer.IsAzerbaijani ? $"{AzDays[(int)date.DayOfWeek]} {date:dd}.{date:MM}"
             : JsonStringLocalizer.IsRussian ? $"{RuDays[(int)date.DayOfWeek]} {date:dd}.{date:MM}"
             : date.ToString("ddd, MMM dd");

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

        [AllowAnonymous]
        [Route("NotFound")]
        public IActionResult NotFound(int? statusCode = null)
        {
            Response.StatusCode = 404;
            return View(new ErrorViewModel
            {
                StatusCode = statusCode ?? 404,
                Message = "The page you're looking for could not be found."
            });
        }
    }
}
