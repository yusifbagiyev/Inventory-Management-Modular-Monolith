using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
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

                    var actionData = new Dictionary<string, object>
                    {
                        ["productId"] = model.ProductId,
                        ["inventoryCode"] = product?.InventoryCode ?? 0,
                        ["productModel"] = product?.Model ?? "",
                        ["productVendor"] = product?.Vendor ?? "",
                        ["fromDepartmentId"] = product?.DepartmentId ?? 0,
                        ["fromDepartmentName"] = fromDepartment?.Name ?? "",
                        ["fromWorker"] = product?.Worker ?? "",
                        ["toDepartmentId"] = model.ToDepartmentId,
                        ["toDepartmentName"] = toDepartment?.Name ?? "",
                        ["toWorker"] = model.ToWorker ?? "",
                        ["notes"] = model.Notes ?? ""
                    };

                    //Add image data if present
                    if (HttpContext.Request.Form.Files.Count > 0)
                    {
                        var imageFile = HttpContext.Request.Form.Files[0];
                        using var ms = new MemoryStream();
                        await imageFile.CopyToAsync(ms);
                        actionData["imageData"] = Convert.ToBase64String(ms.ToArray());
                        actionData["imageFileName"] = imageFile.FileName;
                    }

                    var response = await _apiService.PostFormAsync<RouteViewModel>("api/inventoryroutes/transfer", HttpContext.Request.Form);


                    if (response.IsApprovalRequest)
                    {
                        TempData["Info"] = response.Message;
                        return RedirectToAction(nameof(Index));
                    }
                    else if(response.IsSuccess)
                    {
                        TempData["Success"] = "Product transferred successfully!";
                        return RedirectToAction(nameof(Index));
                    }
                    else
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
                var response = await _apiService.PutAsync<object, bool>($"api/inventoryroutes/{id}/complete", new { });

                if (response.IsSuccess)
                {
                    TempData["Success"] = "Route completed successfully!";
                }
                else
                    TempData["Error"] = response.Message ?? "Failed to complete route";
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
                var response = await _apiService.DeleteAsync($"api/inventoryroutes/{id}");

                if (response.IsApprovalRequest)
                {
                    TempData["Info"] = response.Message;
                    return RedirectToAction(nameof(Index));
                }
                else if (response.IsSuccess)
                {
                    TempData["Success"] = "Route deleted successfully!";
                    return RedirectToAction(nameof(Index));
                }
                else
                    ModelState.AddModelError("", response.Message ?? "Failed to delete route");
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