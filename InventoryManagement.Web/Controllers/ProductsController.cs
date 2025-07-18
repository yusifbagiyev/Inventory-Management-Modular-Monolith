using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
    public class ProductsController : Controller
    {
        private readonly IApiService _apiService;
        public ProductsController(IApiService apiService)
        {
            _apiService = apiService;
        }

        public async Task<IActionResult> Index()
        {
            var products = await _apiService.GetAsync<List<ProductViewModel>>("api/products");
            return View(products ?? []);
        }



        public async Task<IActionResult> Details(int id)
        {
            var product = await _apiService.GetAsync<ProductViewModel>($"api/products/{id}");
            if (product == null)
                return NotFound();

            return View(product);
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
            if (ModelState.IsValid)
            {
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

                //Get the form collection
                var form =HttpContext.Request.Form;

                var result = await _apiService.PostFormAsync<CreateProductDto>("api/products", form,dto);

                if (result != null)
                {
                    //Check if it is a dynamic object with Status property
                    var resultType=result.GetType();
                    var statusProperty = resultType.GetProperty("Status");

                    if (statusProperty != null && statusProperty.GetValue(result)?.ToString() == "PendingApproval")
                    {
                        TempData["Info"] = "Your product creation request has been submitted for approval. You will be notified once it's processed.";
                    }
                    else
                    {
                        TempData["Success"] = "Product created successfully.";
                    }

                    return RedirectToAction(nameof(Index));
                }

                ModelState.AddModelError("", "Failed to create product.");
            }
            await LoadDropdowns(productModel);
            return View(productModel);
        }



        public async Task<IActionResult> Edit(int id)
        {
            var product = await _apiService.GetAsync<ProductViewModel>($"api/products/{id}");
            if (product == null)
                return NotFound();

            await LoadDropdowns(product);
            return View(product);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProductViewModel productModel)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Always use form data for updates
                    var form = HttpContext.Request.Form;
                    var result = await _apiService.PutFormAsync<bool>($"api/products/{id}", form, productModel);

                    if (result)
                    {
                        var resultType = result.GetType();
                        var statusProperty = resultType.GetProperty("Status");

                        if(statusProperty!=null&& statusProperty.GetValue(result)?.ToString() == "PendingApproval")
                        {
                            TempData["Info"] = "Your product update request has been submitted for approval. You will be notified once it's processed.";
                        }
                        else
                        {
                            TempData["Success"] = "Product updated successfully.";
                        }

                        return RedirectToAction(nameof(Index));
                    }

                    ModelState.AddModelError("", "Failed to update product.");
                }
                catch 
                {
                    ModelState.AddModelError("", "An error occurred while updating the product.");
                }
            }

            await LoadDropdowns(productModel);
            return View(productModel);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _apiService.DeleteAsync($"api/products/{id}");
            if (result)
            {
                var resultType = result.GetType();
                var statusProperty = resultType.GetProperty("Status");
                if (statusProperty != null && statusProperty.GetValue(result)?.ToString() == "PendingApproval")
                {
                    TempData["Info"] = "Your product deletion request has been submitted for approval. You will be notified once it's processed.";
                }
            }
            else
            {
                TempData["Error"] = "Failed to delete product.";
            }
            return RedirectToAction(nameof(Index));
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
            catch
            {
                model.Categories = [];
                model.Departments = [];
            }
        }
    }
}