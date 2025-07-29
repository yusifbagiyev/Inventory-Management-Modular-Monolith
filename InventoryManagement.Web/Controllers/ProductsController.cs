using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
    public class ProductsController : BaseController
    {
        private readonly IApiService _apiService;
        public ProductsController(IApiService apiService,ILogger<ProductsController> logger): base(logger)
        {
            _apiService = apiService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var products = await _apiService.GetAsync<List<ProductViewModel>>("api/products");
                return View(products ?? []);
            }
            catch (Exception ex)
            {
                return HandleException(ex, new List<ProductViewModel>());
            }
        }



        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var product = await _apiService.GetAsync<ProductViewModel>($"api/products/{id}");
                if (product == null)
                    return NotFound();

                return View(product);
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }



        public async Task<IActionResult> Create()
        {
            var model = new ProductViewModel();
            await LoadDropdowns(model);
            return View(model);
        }



        [HttpPost]  
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductViewModel productModel)
        {
            if (!ModelState.IsValid)
            {
                await LoadDropdowns(productModel);
                return HandleValidationErrors(productModel);
            }
            var dto = new CreateProductDto
            {
                InventoryCode = productModel.InventoryCode,
                Model = productModel.Model,
                Vendor = productModel.Vendor,
                Worker = productModel.Worker,
                Description = productModel.Description,
                IsWorking = productModel.IsWorking,
                IsActive = productModel.IsActive,
                IsNewItem = productModel.IsNewItem,
                CategoryId = productModel.CategoryId,
                DepartmentId = productModel.DepartmentId
            };

            try
            {
                var form = HttpContext.Request.Form;
                var response = await _apiService.PostFormAsync<dynamic>("api/products", form, dto);

                return HandleApiResponse(response, "Index");
            }
            catch (Exception ex)
            {
                await LoadDropdowns(productModel);
                return HandleException(ex, productModel);
            }
        }



        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var product = await _apiService.GetAsync<ProductViewModel>($"api/products/{id}");
                if (product == null)
                    return NotFound();

                await LoadDropdowns(product);
                return View(product);
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProductViewModel productModel)
        {
            if (!ModelState.IsValid)
            {
                await LoadDropdowns(productModel);
                return HandleValidationErrors(productModel);
            }
            try
            {
                var form = HttpContext.Request.Form;
                var response = await _apiService.PutFormAsync<bool>($"api/products/{id}", form, productModel);

                return HandleApiResponse(response, "Index");
            }
            catch(Exception ex)
            {
                await LoadDropdowns(productModel);
                return HandleException(ex, productModel);
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateInventoryCode([FromBody] UpdateInventoryCodeDto request)
        {
            try
            {
                var response = await _apiService.PutAsync<UpdateInventoryCodeDto, bool>(
                    $"api/products/{request.Id}/inventory-code",
                    new UpdateInventoryCodeDto { InventoryCode = request.InventoryCode });

                if (response.IsSuccess)
                {
                    return Json(new { success = true });
                }
                else
                {
                    return BadRequest(new { error = response.Message });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var response = await _apiService.DeleteAsync($"api/products/{id}");
                return HandleApiResponse(response, "Index");
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }


        private async Task LoadDropdowns(ProductViewModel model)
        {
            try
            {
                var categories = await _apiService.GetAsync<List<CategoryDto>>("api/categories");
                var departments = await _apiService.GetAsync<List<DepartmentDto>>("api/departments");

                model.Categories = categories?.Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                }).ToList() ?? [];

                model.Departments = departments?.Select(d => new SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = d.Name
                }).ToList() ?? [];
            }
            catch(Exception ex)
            {
                _logger?.LogError(ex, "Failed to load dropdowns");
                model.Categories = [];
                model.Departments = [];
            }
        }
    }
}