using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly IApiService _apiService;
        public HomeController(IApiService apiService)
        {
            _apiService = apiService;
        }

        public IActionResult Index()
        {
            return RedirectToAction("Dashboard");
        }

        public async Task<IActionResult> Dashboard()
        {
            var model = new DashboardViewModel();

            try
            {
                //Fetch dashboard data from API
                var products = await _apiService.GetAsync<List<dynamic>>("api/products");
                var routes = await _apiService.GetAsync<List<dynamic>>("api/inventoryroutes");

                if (products != null)
                {
                    model.TotalProducts=products.Count;
                    model.ActiveProducts = products.Count(p =>
                    {
                        try
                        {
                            return p.IsActive == true;
                        }
                        catch
                        {
                            return false;
                        }
                    });
                }

                if (routes != null)
                {
                    model.TotalRoutes = routes.Count;
                    model.PendingTransfers = routes.Count(r =>
                    {
                        try
                        {
                            return r.isCompleted != true;
                        }
                        catch
                        {
                            return false;
                        }
                    });
                }
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Request.Path });
            }

            //Mock data for demo
            model.DepartmentStats =
            [
                new() { DepartmentName = "Warehouse A", ProductCount = 45, ActiveWorkers = 12 },
                new() { DepartmentName = "Warehouse B", ProductCount = 38, ActiveWorkers = 8 },
                new() { DepartmentName = "Office", ProductCount = 22, ActiveWorkers = 15 }
            ];

            model.CategoryDistributions =
            [
                new() { CategoryName = "Electronics", Count = 35, Color = "#3B82F6" },
                new() { CategoryName = "Furniture", Count = 28, Color = "#10B981" },
                new() { CategoryName = "Vehicles", Count = 15, Color = "#F59E0B" },
                new() { CategoryName = "Tools", Count = 22, Color = "#EF4444" }
            ];

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
            return View();
        }
    }
}