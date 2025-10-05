using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Net.NetworkInformation;
using System.Text;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
    public class RoutesController : BaseController
    {
        private readonly IApiService _apiService;
        private readonly IUrlService _urlService;
        public RoutesController(
            IApiService apiService, 
            IUrlService urlService)
            : base()
        {
            _apiService = apiService;
            _urlService = urlService;
        }


        public async Task<IActionResult> Index(
            int? pageNumber = 1,
            int? pageSize = 30,
            string? search = null,
            bool? isCompleted = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            try
            {
                var queryString = new StringBuilder($"?pageNumber={pageNumber}&pageSize={pageSize}");

                if (!string.IsNullOrEmpty(search))
                    queryString.Append($"&search={Uri.EscapeDataString(search)}");

                if (isCompleted.HasValue)
                    queryString.Append($"&isCompleted={isCompleted.Value}");

                if (startDate.HasValue)
                    queryString.Append($"&startDate={startDate.Value:yyyy-MM-dd}");

                if (endDate.HasValue)
                    queryString.Append($"&endDate={endDate.Value:yyyy-MM-dd}");

                // Add ordering to show pending first
                queryString.Append("&orderBy=IsCompleted&ascending=true");

                var routes = await _apiService.GetAsync<PagedResultDto<RouteViewModel>>(
                    $"api/inventoryroutes{queryString}");

                if (routes != null)
                {
                    foreach (var route in routes.Items)
                    {
                        if (!string.IsNullOrEmpty(route.ImageUrl))
                        {
                            route.FullImageUrl = _urlService.GetImageUrl(route.ImageUrl);
                        }
                    }

                    // Calculate actual displayed range
                    var start = ((routes.PageNumber - 1) * routes.PageSize) + 1;
                    var end = Math.Min(routes.PageNumber * routes.PageSize, routes.TotalCount);
                    ViewBag.ShowingStart = start;
                    ViewBag.ShowingEnd = end;
                    ViewBag.TotalCount = routes.TotalCount;
                }

                ViewBag.CurrentFilter = isCompleted;
                ViewBag.StartDate = startDate;
                ViewBag.CurrentSearch = search;
                ViewBag.CurrentStatus = isCompleted;
                ViewBag.EndDate = endDate;
                ViewBag.PageNumber = pageNumber ?? 1;
                ViewBag.PageSize = pageSize ?? 30;

                return View(routes ?? new PagedResultDto<RouteViewModel>());
            }
            catch (Exception ex)
            {
                return HandleException(ex,new PagedResultDto<RouteViewModel>());
            }
        }



        public async Task<IActionResult> Transfer()
        {
            var model = new TransferViewModel();
            await LoadTransferDropdowns(model);
            return View(model);
        }



        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var route = await _apiService.GetAsync<RouteViewModel>($"api/inventoryroutes/{id}");
                if (route == null)
                    return NotFound();

                if (route != null)
                {
                    if (!string.IsNullOrEmpty(route.ImageUrl))
                    {
                        route.FullImageUrl = _urlService.GetImageUrl(route.ImageUrl);
                    }
                }

                // Load departments for dropdown
                var departments = await _apiService.GetAsync<List<DepartmentDto>>("api/departments");
                ViewBag.Departments = departments?.Select(d => new SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = d.Name
                }).ToList() ?? [];

                return View(route);
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [FromForm] UpdateRouteViewModel model)
        {
            try
            {
                var dto = new
                {
                    model.ToDepartmentId,
                    model.ToWorker,
                    model.Notes
                };

                var response = await _apiService.PutFormAsync<bool>($"api/inventoryroutes/{id}", HttpContext.Request.Form, dto);
                return HandleApiResponse(response, "Index");
            }
            catch (Exception ex)
            {
                // Reload departments on error
                var departments = await _apiService.GetAsync<List<DepartmentDto>>("api/departments");
                ViewBag.Departments = departments?.Select(d => new SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = d.Name
                }).ToList() ?? new List<SelectListItem>();

                return HandleException(ex, model);
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Transfer(TransferViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadTransferDropdowns(model);
                return HandleValidationErrors(model);
            }
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

                return HandleApiResponse(response, "Index");
            }
            catch (Exception ex)
            {
                await LoadTransferDropdowns(model);
                return HandleException(ex, model);
            }
        }



        public async Task<IActionResult> Timeline(int productId)
        {
            try
            {
                var routes = await _apiService.GetAsync<List<RouteViewModel>>($"api/inventoryroutes/product/{productId}");
                ViewBag.ProductId = productId;

                if (routes != null)
                {
                    foreach(var route in routes)
                    {
                        if (!string.IsNullOrEmpty(route.ImageUrl))
                        {
                            route.FullImageUrl = _urlService.GetImageUrl(route.ImageUrl);
                        }
                    }
                }
                return View(routes ?? []);
            }
            catch (Exception ex)
            {
                return HandleException(ex, new List<RouteViewModel>());
            }
        }



        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var route = await _apiService.GetAsync<RouteViewModel>($"api/inventoryroutes/{id}");
                if (route == null)
                    return NotFound();
                if (route != null)
                {
                    if(!string.IsNullOrEmpty(route.ImageUrl))
                    {
                        route.FullImageUrl = _urlService.GetImageUrl(route.ImageUrl);
                    }
                }

                return View(route);
            }
            catch (Exception ex)
            {
                return HandleException(ex, new RouteViewModel());
            }
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
                    TempData["Success"] = "Route completed successfully";
                }
                else
                {
                    TempData["Error"] = response.Message ?? "Failed to complete route";
                }
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error completing route");
                TempData["Error"] = "An error occurred while completing the route";
                return RedirectToAction(nameof(Index));  // Add this return
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportToPdf([FromForm] List<int> routeIds)
        {
            try
            {
                if (routeIds == null || !routeIds.Any())
                {
                    TempData["Error"] = "Please select routes to export";
                    return RedirectToAction("Index");
                }

                // For now, let's export as CSV until PDF implementation is ready
                var routes = new List<RouteViewModel>();

                foreach (var id in routeIds)
                {
                    var route = await _apiService.GetAsync<RouteViewModel>($"api/inventoryroutes/{id}");
                    if (route != null)
                        routes.Add(route);
                }

                // Generate CSV content
                var csv = new StringBuilder();
                csv.AppendLine("Date,Type,Product,From,To,Status");

                foreach (var route in routes)
                {
                    csv.AppendLine($"{route.CreatedAt:yyyy-MM-dd HH:mm}," +
                                  $"{route.RouteTypeName}," +
                                  $"{route.InventoryCode} - {route.Model}," +
                                  $"{route.FromDepartmentName ?? "N/A"} ({route.FromWorker ?? "N/A"})," +
                                  $"{route.ToDepartmentName} ({route.ToWorker ?? "N/A"})," +
                                  $"{(route.IsCompleted ? "Completed" : "Pending")}");
                }

                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                return File(bytes, "text/csv", $"routes_export_{DateTime.Now:yyyyMMdd}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting routes");
                TempData["Error"] = "Failed to export routes";
                return RedirectToAction("Index");
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var response = await _apiService.DeleteAsync($"api/inventoryroutes/{id}");

                if (response.IsSuccess)
                {
                    TempData["Success"] = "Route deleted successfully";
                }
                else
                {
                    TempData["Error"] = response.Message ?? "Failed to delete route";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error deleting route");
                TempData["Error"] = "An error occurred while deleting the route";
                return RedirectToAction(nameof(Index));
            }
        }



        private async Task LoadTransferDropdowns(TransferViewModel model)
        {
            try
            {
                var products = await _apiService.GetAsync<PagedResultDto<ProductDto>>("api/products");
                var departments = await _apiService.GetAsync<List<DepartmentDto>>("api/departments");

                model.Products = products?.Items.Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = $"{p.InventoryCode} - {p.Model} ({p.Vendor})"
                }).ToList() ?? [];

                model.Departments = departments?.Select(d => new SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = d.Name
                }).ToList() ?? [];
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load transfer dropdowns");
                model.Products = [];
                model.Departments = [];
            }
        }
    }
}