using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
    public class CategoriesController : Controller
    {
        private readonly IApiService _apiService;
        private readonly ILogger<CategoriesController> _logger;

        public CategoriesController(IApiService apiService, ILogger<CategoriesController> logger)
        {
            _apiService = apiService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var categories = await _apiService.GetAsync<List<CategoryViewModel>>("api/categories");
            return View(categories ?? new List<CategoryViewModel>());
        }

        public IActionResult Create()
        {
            return View(new CategoryViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CategoryViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var result = await _apiService.PostAsync<CategoryViewModel, CategoryViewModel>("api/categories", model);

                    if (result != null)
                    {
                        TempData["Success"] = "Category created successfully!";
                        return RedirectToAction(nameof(Index));
                    }

                    ModelState.AddModelError("", "Failed to create category");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Create category error");
                    ModelState.AddModelError("", "An error occurred");
                }
            }

            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var category = await _apiService.GetAsync<CategoryViewModel>($"api/categories/{id}");

            if (category == null)
                return NotFound();

            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CategoryViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var result = await _apiService.PutAsync<CategoryViewModel, bool>($"api/categories/{id}", model);

                    if (result)
                    {
                        TempData["Success"] = "Category updated successfully!";
                        return RedirectToAction(nameof(Index));
                    }

                    ModelState.AddModelError("", "Failed to update category");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Update category error");
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
                var result = await _apiService.DeleteAsync($"api/categories/{id}");

                if (result)
                {
                    TempData["Success"] = "Category deleted successfully!";
                }
                else
                {
                    TempData["Error"] = "Failed to delete category. It may be in use.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delete category error");
                TempData["Error"] = "An error occurred";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
