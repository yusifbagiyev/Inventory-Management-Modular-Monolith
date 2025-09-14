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


        public async Task<IActionResult> Dashboard(string period = "week")
        {
            var model = new DashboardViewModel();

            try
            {
                // Calculate date range based on period
                var now = DateTime.Now;
                DateTime startDate;
                DateTime endDate = now;

                switch (period.ToLower())
                {
                    case "week":
                        startDate = now.AddDays(-7);
                        break;
                    case "month":
                        startDate = now.AddMonths(-1);
                        break;
                    case "all":
                        startDate = new DateTime(2020, 1, 1);
                        break;
                    default:
                        startDate = now.AddDays(-7);
                        period = "week";
                        break;
                }

                // Fetch all required data
                var productsTask = _apiService.GetAsync<PagedResultDto<ProductViewModel>>(
                    $"api/products?pageSize=10000&pageNumber=1&startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");
                var allProductsTask = _apiService.GetAsync<PagedResultDto<ProductViewModel>>(
                    "api/products?pageSize=10000&pageNumber=1");
                var routesTask = _apiService.GetAsync<PagedResultDto<RouteViewModel>>(
                    $"api/inventoryroutes?pageSize=10000&startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");
                var departmentsTask = _apiService.GetAsync<List<DepartmentViewModel>>("/api/departments");
                var categoriesTask = _apiService.GetAsync<PagedResultDto<CategoryViewModel>>("/api/categories/paged?pageSize=1000");

                await Task.WhenAll(productsTask, allProductsTask, routesTask, departmentsTask, categoriesTask);

                var periodProducts = await productsTask;
                var allProducts = await allProductsTask;
                var routes = await routesTask;
                var departments = await departmentsTask;
                var categoriesResult = await categoriesTask;

                // Set product counts based on period
                if (period.ToLower() == "all")
                {
                    model.TotalProducts = allProducts?.TotalCount ?? 0;
                    model.ActiveProducts = allProducts?.Items.Count(p => p.IsActive) ?? 0;
                }
                else
                {
                    model.TotalProducts = periodProducts?.TotalCount ?? 0;
                    model.ActiveProducts = periodProducts?.Items.Count(p => p.IsActive) ?? 0;
                }

                // Process routes for the period
                if (routes != null)
                {
                    model.TotalRoutes = routes.TotalCount;
                    model.CompletedTransfers = routes.Items.Count(r => r.IsCompleted);
                    model.PendingTransfers = routes.Items.Count(r => !r.IsCompleted);
                }

                // Process departments (show all active departments, but filter transfers by period)
                if (departments != null && allProducts != null)
                {
                    var activeDepartments = departments.Where(d => d.IsActive).ToList();

                    model.DepartmentStats = activeDepartments.Select(d =>
                    {
                        var deptProducts = allProducts.Items.Where(p => p.DepartmentId == d.Id).ToList();
                        var periodTransfers = 0;

                        if (routes != null && period.ToLower() != "all")
                        {
                            periodTransfers = routes.Items
                                .Where(r => r.ToDepartmentId == d.Id)
                                .Count();
                        }

                        return new DepartmentStats
                        {
                            DepartmentName = d.Name,
                            ProductCount = deptProducts.Count,
                            ActiveWorkers = deptProducts
                                .Where(p => !string.IsNullOrEmpty(p.Worker))
                                .Select(p => p.Worker)
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .Count(),
                            PeriodTransfers = periodTransfers
                        };
                    })
                    .OrderByDescending(d => d.ProductCount)
                    .Take(5)
                    .ToList();
                }

                // Process categories (show all categories distribution)
                if (categoriesResult != null && allProducts != null)
                {
                    var categories = categoriesResult.Items.Where(c => c.IsActive).ToList();
                    var colors = new[] { "#3B82F6", "#10B981", "#F59E0B", "#EF4444", "#8B5CF6", "#EC4899", "#06B6D4", "#F97316" };

                    model.CategoryDistributions = categories.Select((c, index) =>
                    {
                        var categoryProducts = allProducts.Items.Count(p => p.CategoryId == c.Id);
                        return new CategoryDistribution
                        {
                            CategoryName = c.Name,
                            Count = categoryProducts,
                            Color = colors[index % colors.Length]
                        };
                    })
                    .Where(c => c.Count > 0)
                    .OrderByDescending(c => c.Count)
                    .Take(8)
                    .ToList();
                }

                ViewBag.CurrentPeriod = period;
                ViewBag.PeriodStartDate = startDate;
                ViewBag.PeriodEndDate = endDate;
                ViewBag.TotalDepartments = departments?.Count(d => d.IsActive) ?? 0;
                ViewBag.TotalCategories = categoriesResult?.TotalCount ?? 0;

                return View(model);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading dashboard");
                return HandleException(ex, new DashboardViewModel());
            }
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