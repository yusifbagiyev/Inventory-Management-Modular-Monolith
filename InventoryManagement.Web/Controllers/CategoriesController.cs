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

        public async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 20, string? search = null)
        {
            try
            {
                var queryString=$"?pageNumber={pageNumber}&pageSize={pageSize}";
                if(!string.IsNullOrWhiteSpace(search))
                    queryString+=$"&search={Uri.EscapeDataString(search)}"; 

                var result=await _apiService.GetAsync<PagedResultDto<CategoryViewModel>>($"api/categories/paged{queryString}");
                ViewBag.CurrentSearch = search;
                return View(result ?? new PagedResultDto<CategoryViewModel>());
            }
            catch (Exception ex)
            {
                return HandleException(ex, new PagedResultDto<CategoryViewModel>());
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
                var products = await _apiService.GetAsync<PagedResultDto<ProductViewModel>>($"api/products?pageSize=1000&categoryId={id}");
                var categoryProducts = products?.Items ?? new List<ProductViewModel>();

                ViewBag.Products = categoryProducts;
                category.ProductCount = categoryProducts.Count();

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
