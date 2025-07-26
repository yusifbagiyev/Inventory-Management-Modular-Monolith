using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
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
                    var response = await _apiService.PostAsync<CategoryViewModel, CategoryViewModel>("api/categories", model);

                    if (response.IsApprovalRequest)
                    {
                        TempData["Info"] = response.Message;
                        return RedirectToAction(nameof(Index));
                    }
                    else if (response.IsSuccess)
                    {
                        TempData["Success"] = "Category created successfully.";
                        return RedirectToAction(nameof(Index));
                    }
                    else
                    {
                        ModelState.AddModelError("", response.Message ?? "Failed to create category");
                    }
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
                    var response = await _apiService.PutAsync<CategoryViewModel, bool>($"api/categories/{id}", model);

                    if (response.IsApprovalRequest)
                    {
                        TempData["Info"] = response.Message;
                        return RedirectToAction(nameof(Index));
                    }
                    else if (response.IsSuccess)
                    {
                        TempData["Success"] = "Category updated successfully.";
                        return RedirectToAction(nameof(Index));
                    }
                    else
                    {
                        ModelState.AddModelError("", response.Message ?? "Failed to update category");
                    }
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
                var response = await _apiService.DeleteAsync($"api/categories/{id}");

                if (response.IsApprovalRequest)
                {
                    TempData["Info"] = response.Message;
                    return RedirectToAction(nameof(Index));
                }
                else if (response.IsSuccess)
                {
                    TempData["Success"] = "Category deleted successfully.";
                }
                else
                {
                    ModelState.AddModelError("", response.Message ?? "Failed to delete category");
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
