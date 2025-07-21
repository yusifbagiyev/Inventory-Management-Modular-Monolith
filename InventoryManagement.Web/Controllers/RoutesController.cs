using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
    public class RoutesController : Controller
    {
        private readonly IApiService _apiService;
        private readonly ILogger<RoutesController> _logger;
        public RoutesController(IApiService apiService, ILogger<RoutesController> logger)
        {
            _apiService= apiService;
            _logger = logger;
        }
        public async Task<IActionResult> Index(int? pageNumber = 1, int? pageSize = 20, bool? isCompleted = null)
        {
            var queryString=$"?pageNumber={pageNumber}&pageSize={pageSize}";
            if (isCompleted.HasValue)
            {
                queryString+=$"&isCompleted={isCompleted.Value}";
            }

            var result = await _apiService.GetAsync<PagedResultDto<RouteViewModel>>($"api/inventoryroutes{queryString}");
            ViewBag.CurrentFilter = isCompleted;
            ViewBag.PageNumber = pageNumber ?? 1;
            ViewBag.PageSize = pageSize ?? 20;

            return View(result ?? new PagedResultDto<RouteViewModel>());
        }

        public async Task<IActionResult> Transfer()
        {
            var model = new TransferViewModel();
            await LoadTransferDropdowns(model);

            ViewBag.JwtToken = HttpContext.Session.GetString("JwtToken");
            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Transfer(TransferViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Get product details first to include in approval request
                    var product = await _apiService.GetAsync<ProductDto>($"api/products/{model.ProductId}");
                    var departments = await _apiService.GetAsync<List<DepartmentDto>>("api/departments");

                    var fromDepartment = departments?.FirstOrDefault(d => d.Id == product?.DepartmentId);
                    var toDepartment = departments?.FirstOrDefault(d => d.Id == model.ToDepartmentId);

                    // Create a form collection with additional data
                    var formCollection = HttpContext.Request.Form;
                    var additionalData = new Dictionary<string, string>
                    {
                        ["inventoryCode"] = product?.InventoryCode.ToString() ?? "",
                        ["fromDepartmentName"] = fromDepartment?.Name ?? "",
                        ["fromDepartmentId"] = product?.DepartmentId.ToString() ?? "",
                        ["toDepartmentName"] = toDepartment?.Name ?? "",
                        ["productModel"] = product?.Model ?? "",
                        ["productVendor"] = product?.Vendor ?? ""
                    };

                    // Add additional data to form
                    var modifiedForm = new FormCollection(
                        formCollection.ToDictionary(x => x.Key, x => x.Value),
                        formCollection.Files
                    );

                    foreach (var item in additionalData)
                    {
                        modifiedForm = new FormCollection(
                            modifiedForm.Union(new[] { new KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues>(item.Key, item.Value) }).ToDictionary(x => x.Key, x => x.Value),
                            modifiedForm.Files
                        );
                    }

                    var result = await _apiService.PostFormAsync<RouteViewModel>("api/inventoryroutes/transfer", modifiedForm);

                    if (result != null)
                    {
                        var resultType = result.GetType();
                        var statusProperty = resultType.GetProperty("Status");

                        if (statusProperty != null && statusProperty.GetValue(result)?.ToString() == "PendingApproval")
                        {
                            TempData["Info"] = "Your transfer request has been submitted for approval. You will be notified once it's processed.";
                        }
                        else
                        {
                            TempData["Success"] = "Product transferred successfully!";
                        }
                        return RedirectToAction(nameof(Index));
                    }

                    ModelState.AddModelError("", "Failed to transfer product");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Transfer error");
                    ModelState.AddModelError("", "An error occurred during transfer");
                }
            }

            await LoadTransferDropdowns(model);
            return View(model);
        }


        public async Task<IActionResult> Timeline(int productId)
        {
            var routes = await _apiService.GetAsync<List<RouteViewModel>>($"api/inventoryroutes/product/{productId}");

            ViewBag.ProductId = productId;
            return View(routes ?? []);
        }


        public async Task<IActionResult> Details(int id)
        {
            var route = await _apiService.GetAsync<RouteViewModel>($"api/inventoryroutes/{id}");

            if (route == null)
                return NotFound();

            return View(route);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id)
        {
            try
            {
                var result = await _apiService.PutAsync<object, bool>($"api/inventoryroutes/{id}/complete", new { });

                if (result)
                {
                    TempData["Success"] = "Route completed successfully!";
                }
                else
                {
                    TempData["Error"] = "Failed to complete route";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Complete route error");
                TempData["Error"] = "An error occurred";
            }

            return RedirectToAction(nameof(Index));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var result = await _apiService.DeleteAsync($"api/inventoryroutes/{id}");

                if (result)
                {
                    TempData["Success"] = "Route deleted successfully!";
                }
                else
                {
                    TempData["Error"] = "Failed to delete route";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delete route error");
                TempData["Error"] = "An error occurred";
            }

            return RedirectToAction(nameof(Index));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchDelete([FromBody] BatchDeleteDto dto)
        {
            try
            {
                var result = await _apiService.PostAsync<BatchDeleteDto, BatchDeleteResultDto>("api/inventoryroutes/batch-delete", dto);

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batch delete error");
                return Json(new { success = false, message = "An error occurred" });
            }
        }


        private async Task LoadTransferDropdowns(TransferViewModel model)
        {
            var products = await _apiService.GetAsync<List<ProductDto>>("api/products");
            var departments = await _apiService.GetAsync<List<DepartmentDto>>("api/departments");

            model.Products = products?.Select(p => new SelectListItem
            {
                Value = p.Id.ToString(),
                Text = $"{p.InventoryCode} - {p.Model} ({p.Vendor})"
            }).ToList() ?? new List<SelectListItem>();

            model.Departments = departments?.Select(d => new SelectListItem
            {
                Value = d.Id.ToString(),
                Text = d.Name
            }).ToList() ?? new List<SelectListItem>();
        }
    }
}