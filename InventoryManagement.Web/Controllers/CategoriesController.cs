using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
    public class CategoriesController : BaseController
    {
        private readonly IApiService _apiService;

        public CategoriesController(IApiService apiService, ILogger<CategoriesController> logger)
            :base(logger)
        {
            _apiService = apiService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var categories = await _apiService.GetAsync<List<CategoryViewModel>>("api/categories");
                return View(categories ?? []);
            }
            catch (Exception ex)
            {
                return HandleException(ex, new List<CategoryViewModel>());
            }
        }


        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var category = await _apiService.GetAsync<CategoryViewModel>($"api/categories/{id}");
                if (category == null)
                    return NotFound();

                // Get products for this category
                var products = await _apiService.GetAsync<PagedResultDto<ProductViewModel>>($"api/products");
                var categoryProducts = products?.Items.Where(p => p.CategoryId == id).ToList() ?? new List<ProductViewModel>();

                ViewBag.Products = categoryProducts;
                category.ProductCount = categoryProducts.Count;

                return View(category);
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }


        public IActionResult Create()
        {
            return View(new CategoryViewModel());
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CategoryViewModel model)
        {
            if(!ModelState.IsValid)
            {
                return HandleValidationErrors(model);
            }
            try
            {
                var dto = new CreateCategoryDto
                {
                    Name = model.Name,
                    Description = model.Description,
                    IsActive = model.IsActive
                };

                var response = await _apiService.PostAsync<CreateCategoryDto, CategoryDto>("api/categories", dto);
                return HandleApiResponse(response, "Index");
            }
            catch (Exception ex)
            {
                return HandleException(ex,model);
            }
        }



        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var category = await _apiService.GetAsync<CategoryViewModel>($"api/categories/{id}");

                if (category == null)
                    return NotFound();

                return View(category);
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CategoryViewModel model)
        {
            if(!ModelState.IsValid)
            {
                return HandleValidationErrors(model);
            }
            try
            {
                var dto = new UpdateCategoryDto
                {
                    Name = model.Name,
                    Description = model.Description,
                    IsActive = model.IsActive
                };
                var response = await _apiService.PutAsync<UpdateCategoryDto, bool>($"api/categories/{id}", dto);
                return HandleApiResponse(response, "Index");
            }
            catch (Exception ex)
            {
                return HandleException(ex, model);
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var response = await _apiService.DeleteAsync($"api/categories/{id}");
                return HandleApiResponse(response, "Index");
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }
    }
}
