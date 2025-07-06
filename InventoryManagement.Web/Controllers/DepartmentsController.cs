using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
    public class DepartmentsController : Controller
    {
        private readonly IApiService _apiService;
        private readonly ILogger<DepartmentsController> _logger;

        public DepartmentsController(IApiService apiService, ILogger<DepartmentsController> logger)
        {
            _apiService = apiService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var departments = await _apiService.GetAsync<List<DepartmentViewModel>>("api/departments");
            return View(departments ?? new List<DepartmentViewModel>());
        }

        public IActionResult Create()
        {
            return View(new DepartmentViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DepartmentViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var result = await _apiService.PostAsync<DepartmentViewModel, DepartmentViewModel>("api/departments", model);

                    if (result != null)
                    {
                        TempData["Success"] = "Department created successfully!";
                        return RedirectToAction(nameof(Index));
                    }

                    ModelState.AddModelError("", "Failed to create department");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Create department error");
                    ModelState.AddModelError("", "An error occurred");
                }
            }

            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var department = await _apiService.GetAsync<DepartmentViewModel>($"api/departments/{id}");

            if (department == null)
                return NotFound();

            return View(department);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, DepartmentViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var result = await _apiService.PutAsync<DepartmentViewModel, bool>($"api/departments/{id}", model);

                    if (result)
                    {
                        TempData["Success"] = "Department updated successfully!";
                        return RedirectToAction(nameof(Index));
                    }

                    ModelState.AddModelError("", "Failed to update department");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Update department error");
                    ModelState.AddModelError("", "An error occurred");
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var result = await _apiService.DeleteAsync($"api/departments/{id}");

                if (result)
                {
                    TempData["Success"] = "Department deleted successfully!";
                }
                else
                {
                    TempData["Error"] = "Failed to delete department. It may have associated products.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delete department error");
                TempData["Error"] = "An error occurred";
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int id)
        {
            var department = await _apiService.GetAsync<DepartmentViewModel>($"api/departments/{id}");

            if (department == null)
                return NotFound();

            // Get products in this department
            var products = await _apiService.GetAsync<List<ProductViewModel>>($"api/products?departmentId={id}");
            ViewBag.Products = products ?? new List<ProductViewModel>();

            return View(department);
        }
    }
}