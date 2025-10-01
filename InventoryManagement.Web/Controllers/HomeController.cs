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
        public HomeController(IApiService apiService,ILogger<HomeController> logger)
            :base(logger)
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


        // Replace the Dashboard action in HomeController.cs with this fixed version

        public async Task<IActionResult> Dashboard(string period = "week")
        {
            var model = new DashboardViewModel();

            try
            {
                // Calculate date range based on period - FIXED LOGIC
                var now = DateTime.Now;
                DateTime startDate;
                DateTime endDate = now;

                switch (period.ToLower())
                {
                    case "week":
                        // Get the last 7 days including today
                        startDate = now.Date.AddDays(-6); // Changed from -7 to -6 to include today
                        break;
                    case "month":
                        // Get the last 30 days
                        startDate = now.Date.AddDays(-29); // Changed to -29 to get exactly 30 days including today
                        break;
                    case "6months":
                        // Get the last 6 months
                        startDate = now.Date.AddMonths(-6);
                        break;
                    case "all":
                        // Get all data from a reasonable start date
                        startDate = new DateTime(2020, 1, 1);
                        break;
                    default:
                        startDate = now.Date.AddDays(-6);
                        period = "week";
                        break;
                }

                // Log the date range for debugging
                _logger?.LogInformation($"Dashboard period: {period}, Start: {startDate:yyyy-MM-dd}, End: {endDate:yyyy-MM-dd}");

                // Get ALL products first (for reference)
                var allProductsTask = _apiService.GetAsync<PagedResultDto<ProductViewModel>>(
                    "api/products?pageSize=10000&pageNumber=1");

                // Get routes for the specific period with proper date formatting
                var routesTask = _apiService.GetAsync<PagedResultDto<RouteViewModel>>(
                    $"api/inventoryroutes?pageSize=10000&pageNumber=1&startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");

                // Get departments and categories
                var departmentsTask = _apiService.GetAsync<List<DepartmentViewModel>>("/api/departments");
                var categoriesTask = _apiService.GetAsync<PagedResultDto<CategoryViewModel>>("/api/categories/paged?pageSize=1000");

                await Task.WhenAll(allProductsTask, routesTask, departmentsTask, categoriesTask);

                var allProducts = await allProductsTask;
                var routes = await routesTask;
                var departments = await departmentsTask;
                var categoriesResult = await categoriesTask;

                // Process products based on period - IMPROVED LOGIC
                if (allProducts != null && allProducts.Items != null)
                {
                    var products = allProducts.Items.ToList();

                    if (period.ToLower() == "all")
                    {
                        // For "all" period, show total products
                        model.TotalProducts = products.Count();
                        model.ActiveProducts = products.Count(p => p.IsActive);
                    }
                    else
                    {
                        // For specific periods, we have two approaches:
                        // 1. Products created in the period (new items)
                        // 2. All active products (current inventory)

                        // Count new products added in this period
                        var newProductsInPeriod = products.Where(p =>
                            p.CreatedAt.HasValue &&
                            p.CreatedAt.Value >= startDate &&
                            p.CreatedAt.Value <= endDate).ToList();

                        // If no new products in period, show current active inventory
                        if (newProductsInPeriod.Any())
                        {
                            model.TotalProducts = newProductsInPeriod.Count();
                            model.ActiveProducts = newProductsInPeriod.Count(p => p.IsActive);
                        }
                        else
                        {
                            // Show current inventory stats with a note that no new products were added
                            model.TotalProducts = 0; // No new products in this period
                            model.ActiveProducts = products.Count(p => p.IsActive); // But show active inventory

                            // Store this for the view to show appropriate message
                            ViewBag.NoNewProducts = true;
                            ViewBag.TotalInventory = products.Count();
                        }

                        // Also provide total inventory count for reference
                        ViewBag.TotalInventoryCount = products.Count();
                        ViewBag.ActiveInventoryCount = products.Count(p => p.IsActive);
                    }
                }

                // Process routes for the period - FIXED
                if (routes != null && routes.Items != null)
                {
                    var allRoutes = routes.Items.ToList();

                    // Filter for transfer routes only
                    var transferRoutes = allRoutes.Where(r =>
                        r.RouteTypeName == "Transfer" ||
                        r.RouteType == "Transfer").ToList();

                    model.TotalRoutes = transferRoutes.Count();
                    model.CompletedTransfers = transferRoutes.Count(r => r.IsCompleted);
                    model.PendingTransfers = transferRoutes.Count(r => !r.IsCompleted);

                    // Log for debugging
                    _logger?.LogInformation($"Routes found: Total={allRoutes.Count()}, Transfers={transferRoutes.Count()}, Completed={model.CompletedTransfers}, Pending={model.PendingTransfers}");
                }
                else
                {
                    model.TotalRoutes = 0;
                    model.CompletedTransfers = 0;
                    model.PendingTransfers = 0;
                }

                // Process departments with better data handling
                if (departments != null && routes != null && routes.Items != null)
                {
                    var activeDepartments = departments.Where(d => d.IsActive).ToList();
                    var routesList = routes.Items.ToList();

                    model.DepartmentStats = activeDepartments.Select(d =>
                    {
                        // Get all routes related to this department in the period
                        var departmentRoutes = routesList.Where(r =>
                            (r.ToDepartmentId == d.Id || r.FromDepartmentId == d.Id) &&
                            r.RouteTypeName == "Transfer").ToList();

                        // Count completed transfers TO this department
                        var completedTransfersTo = routesList
                            .Where(r => r.ToDepartmentId == d.Id &&
                                       r.RouteTypeName == "Transfer" &&
                                       r.IsCompleted)
                            .Count();

                        // Get unique workers in this department from routes
                        var uniqueWorkers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        // Add workers from routes TO this department
                        foreach (var route in routesList.Where(r => r.ToDepartmentId == d.Id))
                        {
                            if (!string.IsNullOrEmpty(route.ToWorker))
                                uniqueWorkers.Add(route.ToWorker.Trim());
                        }

                        // Add workers from routes FROM this department
                        foreach (var route in routesList.Where(r => r.FromDepartmentId == d.Id))
                        {
                            if (!string.IsNullOrEmpty(route.FromWorker))
                                uniqueWorkers.Add(route.FromWorker.Trim());
                        }

                        return new DepartmentStats
                        {
                            DepartmentName = d.Name,
                            ProductCount = completedTransfersTo,
                            ActiveWorkers = uniqueWorkers.Count,
                            PeriodTransfers = departmentRoutes.Count()
                        };
                    })
                    .Where(d => d.PeriodTransfers > 0 || d.ProductCount > 0) // Only show active departments
                    .OrderByDescending(d => d.PeriodTransfers)
                    .ThenByDescending(d => d.ProductCount)
                    .Take(5)
                    .ToList();

                    // If no department activity in period, show top departments by current inventory
                    if (!model.DepartmentStats.Any() && allProducts != null)
                    {
                        var productsList = allProducts?.Items?.ToList() ?? [];
                        model.DepartmentStats = activeDepartments
                            .Select(d => new DepartmentStats
                            {
                                DepartmentName = d.Name,
                                ProductCount = productsList.Count(p => p.DepartmentId == d.Id),
                                ActiveWorkers = productsList
                                    .Where(p => p.DepartmentId == d.Id && !string.IsNullOrEmpty(p.Worker))
                                    .Select(p => p.Worker?.Trim().ToLower())
                                    .Distinct()
                                    .Count(),
                                PeriodTransfers = 0
                            })
                            .Where(d => d.ProductCount > 0)
                            .OrderByDescending(d => d.ProductCount)
                            .Take(5)
                            .ToList();
                    }
                }

                // Process categories - IMPROVED
                if (categoriesResult != null && categoriesResult.Items != null && allProducts != null)
                {
                    var categories = categoriesResult.Items.Where(c => c.IsActive).ToList();
                    var productsList = allProducts?.Items?.ToList() ?? [];

                    var colors = new[] {
                "#3B82F6", "#10B981", "#F59E0B", "#EF4444",
                "#8B5CF6", "#EC4899", "#06B6D4", "#14B8A6"
            };

                    if (period.ToLower() == "all")
                    {
                        // For "all", show total distribution
                        model.CategoryDistributions = categories.Select((c, index) =>
                        {
                            var count = productsList.Count(p => p.CategoryId == c.Id);
                            return new CategoryDistribution
                            {
                                CategoryName = c.Name,
                                Count = count,
                                Color = colors[index % colors.Length]
                            };
                        })
                        .Where(c => c.Count > 0)
                        .OrderByDescending(c => c.Count)
                        .Take(8)
                        .ToList();
                    }
                    else
                    {
                        // For specific periods, show new products by category
                        var newProductsInPeriod = productsList.Where(p =>
                            p.CreatedAt.HasValue &&
                            p.CreatedAt.Value >= startDate &&
                            p.CreatedAt.Value <= endDate).ToList();

                        if (newProductsInPeriod.Any())
                        {
                            model.CategoryDistributions = categories.Select((c, index) =>
                            {
                                var count = newProductsInPeriod.Count(p => p.CategoryId == c.Id);
                                return new CategoryDistribution
                                {
                                    CategoryName = c.Name,
                                    Count = count,
                                    Color = colors[index % colors.Length]
                                };
                            })
                            .Where(c => c.Count > 0)
                            .OrderByDescending(c => c.Count)
                            .Take(8)
                            .ToList();
                        }
                        else
                        {
                            // If no new products, show current distribution
                            model.CategoryDistributions = categories.Select((c, index) =>
                            {
                                var count = productsList.Count(p => p.CategoryId == c.Id && p.IsActive);
                                return new CategoryDistribution
                                {
                                    CategoryName = c.Name,
                                    Count = count,
                                    Color = colors[index % colors.Length]
                                };
                            })
                            .Where(c => c.Count > 0)
                            .OrderByDescending(c => c.Count)
                            .Take(8)
                            .ToList();

                            ViewBag.ShowingCurrentDistribution = true;
                        }
                    }
                }

                // Generate transfer activity data - IMPROVED
                if (routes != null && routes.Items != null)
                {
                    model.TransferActivityData = GenerateTransferActivityData(
                        routes.Items.ToList(),
                        period,
                        startDate,
                        endDate);
                }
                else
                {
                    // Provide empty data structure
                    model.TransferActivityData = new TransferActivityData
                    {
                        Labels = new List<string>(),
                        CompletedData = new List<int>(),
                        PendingData = new List<int>()
                    };
                }

                // Set ViewBag data with additional debugging info
                ViewBag.CurrentPeriod = period;
                ViewBag.PeriodStartDate = startDate;
                ViewBag.PeriodEndDate = endDate;
                ViewBag.TotalDepartments = departments?.Count(d => d.IsActive) ?? 0;
                ViewBag.TotalCategories = categoriesResult?.Items?.Count(c => c.IsActive) ?? 0;


                return View(model);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error loading dashboard for period: {period}");

                // Return a model with default values instead of error page
                model = new DashboardViewModel
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

                ViewBag.ErrorMessage = "Unable to load some dashboard data. Please try refreshing the page.";
                ViewBag.CurrentPeriod = period;
                ViewBag.PeriodStartDate = DateTime.Now.AddDays(-7);
                ViewBag.PeriodEndDate = DateTime.Now;

                return View(model);
            }
        }


        /// <summary>
        /// Generate transfer activity data based on actual route data
        /// </summary>
        private TransferActivityData GenerateTransferActivityData(IEnumerable<RouteViewModel> routes, string period, DateTime startDate, DateTime endDate)
        {
            var labels = new List<string>();
            var completedData = new List<int>();
            var pendingData = new List<int>();

            switch (period.ToLower())
            {
                case "week":
                    // Group by days for the past week
                    for (int i = 6; i >= 0; i--)
                    {
                        var date = DateTime.Now.AddDays(-i);
                        labels.Add(date.ToString("ddd"));
                        
                        var dayRoutes = routes.Where(r => r.CreatedAt.Date == date.Date).ToList();
                        completedData.Add(dayRoutes.Count(r => r.IsCompleted));
                        pendingData.Add(dayRoutes.Count(r => !r.IsCompleted));
                    }
                    break;

                case "month":
                    // Group by weeks for the past month
                    var weekStart = startDate;
                    var weekNumber = 1;
                    
                    while (weekStart < endDate)
                    {
                        var weekEnd = weekStart.AddDays(7);
                        if (weekEnd > endDate) weekEnd = endDate;

                        labels.Add($"Week {weekNumber}");
                        
                        var weekRoutes = routes.Where(r => r.CreatedAt >= weekStart && r.CreatedAt < weekEnd).ToList();
                        completedData.Add(weekRoutes.Count(r => r.IsCompleted));
                        pendingData.Add(weekRoutes.Count(r => !r.IsCompleted));

                        weekStart = weekEnd;
                        weekNumber++;
                    }
                    break;

                case "6months":
                    // Group by months for the past 6 months
                    for (int i = 5; i >= 0; i--)
                    {
                        var monthStart = DateTime.Now.AddMonths(-i).Date;
                        var monthEnd = monthStart.AddMonths(1);
                        
                        labels.Add(monthStart.ToString("MMM"));
                        
                        var monthRoutes = routes.Where(r => r.CreatedAt >= monthStart && r.CreatedAt < monthEnd).ToList();
                        completedData.Add(monthRoutes.Count(r => r.IsCompleted));
                        pendingData.Add(monthRoutes.Count(r => !r.IsCompleted));
                    }
                    break;

                case "all":
                    // Group by quarters for all time
                    var allRoutes = routes.OrderBy(r => r.CreatedAt).ToList();
                    if (allRoutes.Any())
                    {
                        var firstDate = allRoutes.First().CreatedAt;
                        var quarterStart = new DateTime(firstDate.Year, ((firstDate.Month - 1) / 3) * 3 + 1, 1);
                        
                        while (quarterStart < endDate)
                        {
                            var quarterEnd = quarterStart.AddMonths(3);
                            labels.Add($"Q{((quarterStart.Month - 1) / 3) + 1} {quarterStart.Year}");
                            
                            var quarterRoutes = routes.Where(r => r.CreatedAt >= quarterStart && r.CreatedAt < quarterEnd).ToList();
                            completedData.Add(quarterRoutes.Count(r => r.IsCompleted));
                            pendingData.Add(quarterRoutes.Count(r => !r.IsCompleted));
                            
                            quarterStart = quarterEnd;
                        }
                    }
                    break;
            }

            return new TransferActivityData
            {
                Labels = labels,
                CompletedData = completedData,
                PendingData = pendingData
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

            // Check if this is an AJAX request
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
    }
}