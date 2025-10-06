using System.Diagnostics;
using InventoryManagement.Web.Models;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
    public class HomeController : BaseController
    {
        private readonly IApiService _apiService;

        public HomeController(IApiService apiService, ILogger<HomeController> logger)
            : base(logger)
        {
            _apiService = apiService;
        }



        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated ?? false)
            {
                return RedirectToAction("Dashboard");
            }
            return View();
        }



        public async Task<IActionResult> Dashboard(string period = "last7days")
        {
            var model = new DashboardViewModel();

            try
            {
                // Add validation for period parameter
                var validPeriods = new[] { "last7days", "last30days", "last6months", "all" };
                if (!validPeriods.Contains(period.ToLower()))
                {
                    period = "last7days";
                }

                // Calculate date range with proper period handling
                var now = DateTime.Now;
                DateTime startDate;
                DateTime endDate = now.Date.AddDays(1).AddSeconds(-1); // End of today

                switch (period.ToLower())
                {
                    case "last7days":
                        startDate = now.Date.AddDays(-6); // Last 7 days including today
                        break;
                    case "last30days":
                        startDate = now.Date.AddDays(-29); // Last 30 days including today
                        break;
                    case "last6months":
                        startDate = now.Date.AddMonths(-6);
                        break;
                    case "all":
                        startDate = new DateTime(2020, 1, 1);
                        break;
                    default:
                        startDate = now.Date.AddDays(-6);
                        period = "last7days";
                        break;
                }
                // Fetch all data in parallel for better performance
                var allProductsTask = _apiService.GetAsync<PagedResultDto<ProductViewModel>>(
                    "api/products?pageSize=10000&pageNumber=1");

                var allRoutesTask = _apiService.GetAsync<PagedResultDto<RouteViewModel>>(
                    "api/inventoryroutes?pageSize=10000&pageNumber=1");

                var departmentsTask = _apiService.GetAsync<List<DepartmentViewModel>>("/api/departments");

                var categoriesTask = _apiService.GetAsync<PagedResultDto<CategoryViewModel>>(
                    "/api/categories/paged?pageSize=1000");

                await Task.WhenAll(allProductsTask, allRoutesTask, departmentsTask, categoriesTask);

                var allProducts = await allProductsTask;
                var allRoutes = await allRoutesTask;
                var departments = await departmentsTask;
                var categoriesResult = await categoriesTask;

                // Filter routes by period first - this is our primary data source
                var routesInPeriod = FilterRoutesByPeriod(allRoutes, startDate, endDate);

                // Process all metrics based on routes in the period
                ProcessProductMetrics(model, allProducts, startDate, endDate, period);
                ProcessRouteMetrics(model, routesInPeriod, period);
                ProcessDepartmentStats(model, routesInPeriod, departments, allProducts);
                ProcessCategoryDistribution(model, routesInPeriod, allProducts, categoriesResult);
                ProcessTransferActivity(model, routesInPeriod, startDate, endDate, period);

                // Calculate active categories and departments based on transfer activity
                var activeCategoriesInPeriod = CalculateActiveCategoriesCount(routesInPeriod, allProducts);
                var activeDepartmentsInPeriod = CalculateActiveDepartmentsCount(routesInPeriod);

                // Set ViewBag data
                ViewBag.CurrentPeriod = period;
                ViewBag.PeriodStartDate = startDate;
                ViewBag.PeriodEndDate = endDate;
                ViewBag.TotalDepartments = departments?.Count(d => d.IsActive) ?? 0;
                ViewBag.TotalCategories = categoriesResult?.Items?.Count(c => c.IsActive) ?? 0;
                ViewBag.ActiveDepartmentsInPeriod = activeDepartmentsInPeriod;
                ViewBag.ActiveCategoriesInPeriod = activeCategoriesInPeriod;

                return View(model);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading dashboard for period: {Period}", period);
                return View(GetEmptyDashboard(period));
            }
        }



        /// <summary>
        /// Filter routes by the selected date period
        /// This is the foundation for all period-based metrics
        /// </summary>
        private List<RouteViewModel> FilterRoutesByPeriod(
            PagedResultDto<RouteViewModel>? allRoutes,
            DateTime startDate,
            DateTime endDate)
        {
            if (allRoutes?.Items == null)
                return new List<RouteViewModel>();

            // Filter for Transfer routes within the date range
            // Using .Date property ensures we compare only the date part, ignoring time/microseconds
            return allRoutes.Items
                .Where(r => (r.RouteTypeName == "Transfer" || r.RouteType == "Transfer") &&
                           r.CreatedAt.Date >= startDate.Date &&
                           r.CreatedAt.Date <= endDate.Date)
                .ToList();
        }



        /// <summary>
        /// Calculate how many unique categories were involved in transfers during the period
        /// A category is "active" if at least one product from that category was transferred
        /// </summary>
        private int CalculateActiveCategoriesCount(
            List<RouteViewModel> routesInPeriod,
            PagedResultDto<ProductViewModel>? allProducts)
        {
            if (!routesInPeriod.Any() || allProducts?.Items == null)
                return 0;

            var products = allProducts.Items.ToList();

            // Get unique category IDs from products that were transferred
            var activeCategoryIds = routesInPeriod
                .Select(r => r.ProductId)
                .Distinct()
                .Select(productId => products.FirstOrDefault(p => p.Id == productId)?.CategoryId)
                .Where(categoryId => categoryId.HasValue)
                .Distinct()
                .Count();

            return activeCategoryIds;
        }



        /// <summary>
        /// Calculate how many unique departments were involved in transfers during the period
        /// A department is "active" if it sent or received at least one transfer
        /// </summary>
        private int CalculateActiveDepartmentsCount(List<RouteViewModel> routesInPeriod)
        {
            if (!routesInPeriod.Any())
                return 0;

            // Collect all department IDs that appear as either source or destination
            var activeDepartmentIds = new HashSet<int>();

            foreach (var route in routesInPeriod)
            {
                if (route.FromDepartmentId.HasValue)
                    activeDepartmentIds.Add(route.FromDepartmentId.Value);

                activeDepartmentIds.Add(route.ToDepartmentId);
            }

            return activeDepartmentIds.Count;
        }



        /// <summary>
        /// Process product metrics for the selected period
        /// Shows new products added during the period
        /// </summary>
        private void ProcessProductMetrics(
            DashboardViewModel model,
            PagedResultDto<ProductViewModel>? allProducts,
            DateTime startDate,
            DateTime endDate,
            string period)
        {
            if (allProducts?.Items == null)
            {
                model.TotalProducts = 0;
                model.ActiveProducts = 0;
                return;
            }

            var products = allProducts.Items.ToList();

            if (period.ToLower() == "all")
            {
                // For "all time", show total inventory
                model.TotalProducts = products.Count;
                model.ActiveProducts = products.Count(p => p.IsActive);
            }
            else
            {
                // For specific periods, show NEW products added during this time
                var newProducts = products.Where(p =>
                    p.CreatedAt.HasValue &&
                    p.CreatedAt.Value.Date >= startDate.Date &&
                    p.CreatedAt.Value.Date <= endDate.Date).ToList();

                model.TotalProducts = newProducts.Count;
                model.ActiveProducts = newProducts.Count(p => p.IsActive);

                // Store total inventory for reference
                ViewBag.TotalInventoryCount = products.Count;
                ViewBag.ActiveInventoryCount = products.Count(p => p.IsActive);
            }
        }



        /// <summary>
        /// Process route metrics based on filtered routes
        /// </summary>
        private void ProcessRouteMetrics(
            DashboardViewModel model,
            List<RouteViewModel> routesInPeriod,
            string period)
        {
            model.TotalRoutes = routesInPeriod.Count;
            model.CompletedTransfers = routesInPeriod.Count(r => r.IsCompleted);
            model.PendingTransfers = routesInPeriod.Count(r => !r.IsCompleted);
        }



        /// <summary>
        /// Process department statistics based on transfer activity
        /// Shows which departments were most active during the period
        /// </summary>
        private void ProcessDepartmentStats(
            DashboardViewModel model,
            List<RouteViewModel> routesInPeriod,
            List<DepartmentViewModel>? departments,
            PagedResultDto<ProductViewModel>? allProducts)
        {
            if (departments == null || !routesInPeriod.Any())
            {
                model.DepartmentStats = new List<DepartmentStats>();
                return;
            }

            var products = allProducts?.Items?.ToList() ?? new List<ProductViewModel>();
            var activeDepartments = departments.Where(d => d.IsActive).ToList();

            model.DepartmentStats = activeDepartments.Select(dept =>
            {
                // Get all routes involving this department (as source or destination)
                var deptRoutes = routesInPeriod.Where(r =>
                    r.ToDepartmentId == dept.Id || r.FromDepartmentId == dept.Id).ToList();

                // Count unique products transferred to/from this department
                var uniqueProductIds = deptRoutes
                    .Select(r => r.ProductId)
                    .Distinct()
                    .ToHashSet();

                // Count unique workers in this department during transfers
                var uniqueWorkers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var route in deptRoutes.Where(r => r.ToDepartmentId == dept.Id))
                {
                    if (!string.IsNullOrWhiteSpace(route.ToWorker))
                        uniqueWorkers.Add(route.ToWorker.Trim());
                }

                foreach (var route in deptRoutes.Where(r => r.FromDepartmentId == dept.Id))
                {
                    if (!string.IsNullOrWhiteSpace(route.FromWorker))
                        uniqueWorkers.Add(route.FromWorker.Trim());
                }

                return new DepartmentStats
                {
                    DepartmentName = dept.Name,
                    ProductCount = uniqueProductIds.Count,
                    ActiveWorkers = uniqueWorkers.Count,
                    PeriodTransfers = deptRoutes.Count
                };
            })
            .Where(d => d.PeriodTransfers > 0) // Only show departments with activity
            .OrderByDescending(d => d.PeriodTransfers)
            .ThenByDescending(d => d.ProductCount)
            .Take(5)
            .ToList();
        }



        /// <summary>
        /// Process category distribution based on transfer activity
        /// Shows which categories were involved in transfers during the period
        /// KEY CHANGE: We look at categories of products that were TRANSFERRED, not created
        /// </summary>
        private void ProcessCategoryDistribution(
            DashboardViewModel model,
            List<RouteViewModel> routesInPeriod,
            PagedResultDto<ProductViewModel>? allProducts,
            PagedResultDto<CategoryViewModel>? categoriesResult)
        {
            if (categoriesResult?.Items == null || allProducts?.Items == null)
            {
                model.CategoryDistributions = new List<CategoryDistribution>();
                return;
            }

            var categories = categoriesResult.Items.Where(c => c.IsActive).ToList();
            var products = allProducts.Items.ToList();

            // Professional color palette with maximum contrast and distinction
            // These colors are chosen to be as visually different as possible
            var professionalColors = new[]
            {
                "#FF6B6B", // Coral Red - warm and inviting
                "#4ECDC4", // Turquoise - fresh and clear
                "#FFD93D", // Golden Yellow - bright and cheerful
                "#6BCF7F", // Fresh Green - natural and positive
                "#FF8C42", // Tangerine Orange - energetic
                "#A78BFA", // Soft Purple - creative and modern
                "#FF6BB5", // Pink Rose - friendly and distinct
                "#4DA8FF", // Sky Blue - calm and clear
                "#7FD1AE", // Mint Green - refreshing
                "#FFB84D", // Warm Amber - inviting
                "#B4A7D6", // Lavender - soft but distinct
                "#FF8585", // Salmon - warm alternative to red
                "#5DADE2", // Ocean Blue - professional
                "#82E0AA", // Light Emerald - bright green
                "#F8B739"  // Sunflower Yellow - vibrant gold
            };

            if (!routesInPeriod.Any())
            {
                // No transfers in period
                model.CategoryDistributions = new List<CategoryDistribution>();
                return;
            }

            // Get product IDs that were transferred during the period
            var transferredProductIds = routesInPeriod
                .Select(r => r.ProductId)
                .Distinct()
                .ToHashSet();

            // Find categories of transferred products
            var transferredProducts = products
                .Where(p => transferredProductIds.Contains(p.Id))
                .ToList();

            // Count how many products from each category were transferred
            var categoryCounts = transferredProducts
                .GroupBy(p => p.CategoryId)
                .ToDictionary(g => g.Key, g => g.Count());

            model.CategoryDistributions = categories
                .Where(c => categoryCounts.ContainsKey(c.Id)) // Only categories with transfers
                .Select((category, index) =>
                {
                    return new CategoryDistribution
                    {
                        CategoryName = category.Name,
                        Count = categoryCounts[category.Id],
                        Color = professionalColors[index % professionalColors.Length]
                    };
                })
                .OrderByDescending(c => c.Count)
                .Take(8)
                .ToList();

            _logger?.LogInformation(
                "Category distribution: {Count} categories had transfer activity",
                model.CategoryDistributions.Count);
        }




        /// <summary>
        /// Generate transfer activity data for charts
        /// Groups transfers by appropriate time periods
        /// </summary>
        private void ProcessTransferActivity(
            DashboardViewModel model,
            List<RouteViewModel> routesInPeriod,
            DateTime startDate,
            DateTime endDate,
            string period)
        {
            var labels = new List<string>();
            var completedData = new List<int>();
            var pendingData = new List<int>();

            if (!routesInPeriod.Any())
            {
                model.TransferActivityData = new TransferActivityData
                {
                    Labels = labels,
                    CompletedData = completedData,
                    PendingData = pendingData
                };
                return;
            }

            switch (period.ToLower())
            {
                case "last7days":
                    // Show each of the last 7 days
                    for (int i = 6; i >= 0; i--)
                    {
                        var date = DateTime.Now.Date.AddDays(-i);
                        labels.Add(date.ToString("ddd, MMM dd"));

                        var dayRoutes = routesInPeriod.Where(r => r.CreatedAt.Date == date).ToList();
                        completedData.Add(dayRoutes.Count(r => r.IsCompleted));
                        pendingData.Add(dayRoutes.Count(r => !r.IsCompleted));
                    }
                    break;

                case "last30days":
                    // Group by weeks (approximately 4-5 weeks)
                    var currentWeekStart = startDate;
                    int weekNumber = 1;

                    while (currentWeekStart <= endDate)
                    {
                        var weekEnd = currentWeekStart.AddDays(7);
                        if (weekEnd > endDate) weekEnd = endDate;

                        labels.Add($"Week {weekNumber}");

                        var weekRoutes = routesInPeriod.Where(r =>
                            r.CreatedAt.Date >= currentWeekStart.Date &&
                            r.CreatedAt.Date < weekEnd.Date).ToList();

                        completedData.Add(weekRoutes.Count(r => r.IsCompleted));
                        pendingData.Add(weekRoutes.Count(r => !r.IsCompleted));

                        currentWeekStart = currentWeekStart.AddDays(7); // Changed from weekEnd
                        weekNumber++;

                        // Safety check to prevent infinite loop
                        if (weekNumber > 10) break;
                    }
                    break;

                case "last6months":
                    // Show each of the last 6 months
                    for (int i = 5; i >= 0; i--)
                    {
                        var monthStart = DateTime.Now.AddMonths(-i).Date;
                        var monthEnd = monthStart.AddMonths(1);

                        labels.Add(monthStart.ToString("MMM yyyy"));

                        var monthRoutes = routesInPeriod.Where(r =>
                            r.CreatedAt.Date >= monthStart &&
                            r.CreatedAt.Date < monthEnd).ToList();

                        completedData.Add(monthRoutes.Count(r => r.IsCompleted));
                        pendingData.Add(monthRoutes.Count(r => !r.IsCompleted));
                    }
                    break;

                case "all":
                    // Group by quarters
                    var firstDate = routesInPeriod.Min(r => r.CreatedAt);
                    var quarterStart = new DateTime(firstDate.Year, ((firstDate.Month - 1) / 3) * 3 + 1, 1);

                    while (quarterStart <= endDate)
                    {
                        var quarterEnd = quarterStart.AddMonths(3);
                        var quarter = ((quarterStart.Month - 1) / 3) + 1;

                        labels.Add($"Q{quarter} {quarterStart.Year}");

                        var quarterRoutes = routesInPeriod.Where(r =>
                            r.CreatedAt.Date >= quarterStart &&
                            r.CreatedAt.Date < quarterEnd).ToList();

                        completedData.Add(quarterRoutes.Count(r => r.IsCompleted));
                        pendingData.Add(quarterRoutes.Count(r => !r.IsCompleted));

                        quarterStart = quarterEnd;
                    }
                    break;
            }

            model.TransferActivityData = new TransferActivityData
            {
                Labels = labels,
                CompletedData = completedData,
                PendingData = pendingData
            };
        }



        /// <summary>
        /// Create an empty dashboard model for error cases
        /// </summary>
        private DashboardViewModel GetEmptyDashboard(string period)
        {
            return new DashboardViewModel
            {
                TotalProducts = 0,
                ActiveProducts = 0,
                TotalRoutes = 0,
                PendingTransfers = 0,
                CompletedTransfers = 0,
                DepartmentStats = new List<DepartmentStats>(),
                CategoryDistributions = new List<CategoryDistribution>(),
                TransferActivityData = new TransferActivityData
                {
                    Labels = new List<string>(),
                    CompletedData = new List<int>(),
                    PendingData = new List<int>()
                }
            };
        }



        [AllowAnonymous]
        public IActionResult Privacy()
        {
            return View();
        }



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
                    message = "An error occurred while processing your request",
                    requestId = errorViewModel.RequestId
                });
            }

            return View(errorViewModel);
        }



        [AllowAnonymous]
        [Route("NotFound")]
        public IActionResult NotFound(int? statusCode = null)
        {
            var model = new ErrorViewModel
            {
                StatusCode = statusCode ?? 404,
                Message = "The page you're looking for could not be found."
            };

            Response.StatusCode = 404;
            return View(model);
        }
    }
}