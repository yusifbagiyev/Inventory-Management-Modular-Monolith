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


        public async Task<IActionResult> Dashboard(string period="week")
        {
            var model = new DashboardViewModel();

            try
            {
                // Calculate date range based on period
                var now = DateTime.Now;
                DateTime startDate;
                DateTime endDate=now;

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

                // Fetch all data - products, routes, departments, categories
                var productsTask = _apiService.GetAsync<PagedResultDto<ProductViewModel>>(
                    "api/products?pageSize=10000");
                var routesTask = _apiService.GetAsync<PagedResultDto<RouteViewModel>>(
                    $"api/inventoryroutes?pageSize=10000&startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");
                var departmentsTask = _apiService.GetAsync<List<DepartmentViewModel>>("/api/departments");
                var categoriesTask = _apiService.GetAsync<List<CategoryViewModel>>("/api/categories");

                await Task.WhenAll(productsTask, routesTask, departmentsTask, categoriesTask);

                var products = await productsTask;
                var routes = await routesTask;
                var departments = await departmentsTask;
                var categories = await categoriesTask;

                if (products != null)
                {
                    model.TotalProducts = products.TotalCount;
                    model.ActiveProducts = products.Items.Count(p => p.IsActive);
                }

                if (routes != null) 
                {
                    var periodRoutes=routes.Items
                        .Where(r => r.CreatedAt >= startDate && r.CreatedAt <= endDate)
                        .ToList();

                    model.TotalRoutes = periodRoutes.Count;
                    model.CompletedTransfers = periodRoutes.Count(r => r.IsCompleted);
                    model.PendingTransfers = periodRoutes.Count(r => !r.IsCompleted);
                }

                if (departments != null && products != null)
                {
                    var activeDepartments = departments.Where(d => d.IsActive).ToList();

                    model.DepartmentStats = activeDepartments.Select(d =>
                    {
                        var deptProducts = products.Items.Where(p => p.DepartmentId == d.Id).ToList();
                        var periodTransfers = routes?.Items
                            .Where(r => r.ToDepartmentId == d.Id &&
                                       r.CreatedAt >= startDate &&
                                       r.CreatedAt <= endDate)
                            .Count() ?? 0;

                        return new DepartmentStats
                        {
                            DepartmentName = d.Name,
                            ProductCount = deptProducts.Count,
                            ActiveWorkers = deptProducts
                                .Where(p => !string.IsNullOrEmpty(p.Worker))
                                .Select(p => p.Worker)
                                .Distinct()
                                .Count(),
                            PeriodTransfers = periodTransfers
                        };
                    })
                       .OrderByDescending(d => d.ProductCount)
                       .Take(5)
                       .ToList();
                }

                if (categories != null && products != null)
                {
                    var activeCategories = categories.Where(c => c.IsActive).ToList();
                    var colors = new[] { "#3B82F6", "#10B981", "#F59E0B", "#EF4444", "#8B5CF6", "#EC4899" };

                    model.CategoryDistributions = activeCategories.Select((c, index) =>
                    {
                        var categoryProducts = products.Items.Count(p => p.CategoryId == c.Id);
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
            }
            catch (Exception ex)
            {
                return HandleException(ex, new DashboardViewModel());
            }

            return View(model);
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