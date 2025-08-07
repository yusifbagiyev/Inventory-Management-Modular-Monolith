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
                //Fetch dashboard data from API
                var products = await _apiService.GetAsync<PagedResultDto<ProductViewModel>>("api/products");
                var routes = await _apiService.GetAsync<PagedResultDto<RouteViewModel>>("api/inventoryroutes?pageNumber=1&pageSize=1000");
                var departments = await _apiService.GetAsync<List<DepartmentViewModel>>("/api/departments");
                var categories = await _apiService.GetAsync<List<CategoryViewModel>>("/api/categories");

                if (products != null)
                {
                    model.TotalProducts=products.TotalCount;
                    model.ActiveProducts = products.Items.Count(p => p.IsActive);
                }

                if (routes != null)
                {
                    model.TotalRoutes = routes.TotalCount;

                    var now = DateTime.Now;
                    DateTime startDate = period switch
                    {
                        "week" => now.AddDays(-7),
                        "month" => now.AddMonths(-1),
                        _ => now.Date
                    };

                    var periodRoutes = routes.Items.Where(r => r.CreatedAt >= startDate).ToList();
                    model.CompletedTransfers = periodRoutes.Count(r => r.IsCompleted);
                    model.PendingTransfers = periodRoutes.Count(r => !r.IsCompleted);
                }

                if (departments != null && products != null)
                {
                    model.DepartmentStats = departments.Select(d => new DepartmentStats
                    {
                        DepartmentName = d.Name,
                        ProductCount = products.Items.Count(p => p.DepartmentId == d.Id),
                        ActiveWorkers = products.Items.Where(p => p.DepartmentId == d.Id && !string.IsNullOrEmpty(p.Worker))
                                              .Select(p => p.Worker).Distinct().Count()
                    }).OrderByDescending(d => d.ProductCount).Take(5).ToList();
                }

                if (categories != null && products != null)
                {
                    var colors = new[] { "#3B82F6", "#10B981", "#F59E0B", "#EF4444", "#8B5CF6", "#EC4899" };
                    model.CategoryDistributions = categories.Select((c, index) => new CategoryDistribution
                    {
                        CategoryName = c.Name,
                        Count = products.Items.Count(p => p.CategoryId == c.Id),
                        Color = colors[index % colors.Length]
                    }).Where(c => c.Count > 0).OrderByDescending(c => c.Count).Take(8).ToList();
                }
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Request.Path });
            }
            catch(Exception ex)
            {
                return HandleException(ex,new DashboardViewModel());
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