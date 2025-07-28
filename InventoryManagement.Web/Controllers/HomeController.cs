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
            catch(Exception ex)
            {
                return HandleException(ex,new DashboardViewModel());
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