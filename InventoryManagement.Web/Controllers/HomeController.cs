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


        public async Task<IActionResult> Dashboard(string period="today")
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
                        startDate=now.AddDays(-7);
                        break;
                    case "month":
                        startDate = now.AddDays(-30);
                        break;
                    case "today":
                    default:
                        startDate = now.Date;
                        endDate = now.Date.AddDays(1).AddSeconds(-1);
                        break;
                }
                var productsQuery = $"api/products?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}";

                var products = await _apiService.GetAsync<PagedResultDto<ProductViewModel>>(productsQuery);

                // Fetch routes with date filtering
                var routesQuery = $"api/inventoryroutes?pageNumber=1&pageSize=1000&startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}";
                var routes = await _apiService.GetAsync<PagedResultDto<RouteViewModel>>(routesQuery);

                // Fetch all departments and categories (these are relatively static)
                var departments = await _apiService.GetAsync<List<DepartmentViewModel>>("/api/departments");
                var categories = await _apiService.GetAsync<List<CategoryViewModel>>("/api/categories");

                if (products != null)
                {
                    model.TotalProducts = products.TotalCount;
                    model.ActiveProducts = products.Items.Count(p => p.IsActive);

                    // For period-specific product stats, filter by CreatedAt if available
                    if (period != "today") // For week/month, show new products added in that period
                    {
                        var periodProducts = products.Items.Where(p =>
                            p.CreatedAt.HasValue && p.CreatedAt.Value >= startDate).ToList();

                        // You might want to add these to your DashboardViewModel
                        ViewBag.NewProductsInPeriod = periodProducts.Count;
                    }
                }

                if (routes != null)
                {
                    // Routes are already filtered by date in the API call
                    model.TotalRoutes = routes.TotalCount;
                    model.CompletedTransfers = routes.Items.Count(r => r.IsCompleted);
                    model.PendingTransfers = routes.Items.Count(r => !r.IsCompleted);

                    // Store period-specific counts for display
                    ViewBag.PeriodRoutesCount = routes.TotalCount;
                }

                if (departments != null && products != null)
                {
                    // For department stats, we can show activity within the period
                    if (routes != null && routes.Items.Any())
                    {
                        // Calculate department activity based on routes in the period
                        var departmentActivity = routes.Items
                            .GroupBy(r => r.ToDepartmentId)
                            .Select(g => new
                            {
                                DepartmentId = g.Key,
                                TransferCount = g.Count(),
                                CompletedCount = g.Count(r => r.IsCompleted)
                            })
                            .ToList();

                        model.DepartmentStats = departments.Select(d => new DepartmentStats
                        {
                            DepartmentName = d.Name,
                            ProductCount = products.Items.Count(p => p.DepartmentId == d.Id),
                            ActiveWorkers = products.Items
                                .Where(p => p.DepartmentId == d.Id && !string.IsNullOrEmpty(p.Worker))
                                .Select(p => p.Worker)
                                .Distinct()
                                .Count(),
                            // Add period-specific activity
                            PeriodTransfers = departmentActivity
                                .FirstOrDefault(a => a.DepartmentId == d.Id)?.TransferCount ?? 0
                        })
                        .OrderByDescending(d => d.PeriodTransfers) // Order by period activity
                        .ThenByDescending(d => d.ProductCount)
                        .Take(5)
                        .ToList();
                    }
                    else
                    {
                        // Fallback to regular stats if no routes in period
                        model.DepartmentStats = departments.Select(d => new DepartmentStats
                        {
                            DepartmentName = d.Name,
                            ProductCount = products.Items.Count(p => p.DepartmentId == d.Id),
                            ActiveWorkers = products.Items
                                .Where(p => p.DepartmentId == d.Id && !string.IsNullOrEmpty(p.Worker))
                                .Select(p => p.Worker)
                                .Distinct()
                                .Count()
                        })
                        .OrderByDescending(d => d.ProductCount)
                        .Take(5)
                        .ToList();
                    }
                }

                if (categories != null && products != null)
                {
                    var colors = new[] { "#3B82F6", "#10B981", "#F59E0B", "#EF4444", "#8B5CF6", "#EC4899" };
                    model.CategoryDistributions = categories.Select((c, index) => new CategoryDistribution
                    {
                        CategoryName = c.Name,
                        Count = products.Items.Count(p => p.CategoryId == c.Id),
                        Color = colors[index % colors.Length]
                    })
                    .Where(c => c.Count > 0)
                    .OrderByDescending(c => c.Count)
                    .Take(8)
                    .ToList();
                }

                // Store the period for the view
                ViewBag.CurrentPeriod = period;
                ViewBag.PeriodStartDate = startDate;
                ViewBag.PeriodEndDate = endDate;
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Request.Path });
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