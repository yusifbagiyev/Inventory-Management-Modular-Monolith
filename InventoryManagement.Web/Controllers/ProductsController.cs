using InventoryManagement.Web.Extensions;
using InventoryManagement.Web.Filters;
using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductService.Application.Features.Lookups;
using ProductService.Application.Features.Products.Commands;
using ProductService.Application.Features.Products.Queries;
using ProductService.Application.Interfaces;
using RouteService.Application.Features.Routes.Queries;
using SharedServices.Identity;
using ModuleDtos = ProductService.Application.DTOs;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
    public class ProductsController : BaseController
    {
        private readonly IMediator _mediator;
        private readonly IProductManagementService _productManagement;

        public ProductsController(
            IMediator mediator,
            IProductManagementService productManagement,
            ILogger<ProductsController> logger)
            : base(logger)
        {
            _mediator = mediator;
            _productManagement = productManagement;
        }

        public async Task<IActionResult> Index(
            int? pageNumber = 1,
            int? pageSize = 30,
            string? search = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            bool? status = null,
            bool? availability = null,
            int? categoryId = null,
            int? departmentId = null,
            bool? hasImage = null,
            bool? assigned = null)
        {
            var result = await _mediator.Send(new GetAllProductsQuery(
                pageNumber, pageSize, search, startDate, endDate,
                status, availability, categoryId, departmentId, hasImage, assigned));
            var products = ModelMapper.Map<PagedResultDto<ProductViewModel>>(result);
            var pending = await _mediator.Send(new ApprovalService.Application.Features.Queries.GetPendingProductRequests.Query());
            foreach (var p in products.Items)
                p.PendingRequestId = pending.TryGetValue(p.Id, out var requestId) ? requestId : null;

            ViewBag.ShowingStart = ((products.PageNumber - 1) * products.PageSize) + 1;
            ViewBag.ShowingEnd = Math.Min(products.PageNumber * products.PageSize, products.TotalCount);
            ViewBag.TotalCount = products.TotalCount;

            ViewBag.PageNumber = pageNumber ?? 1;
            ViewBag.PageSize = pageSize ?? 30;
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentAvailability = availability;
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.CurrentCategoryId = categoryId;
            ViewBag.CurrentDepartmentId = departmentId;
            ViewBag.CurrentHasImage = hasImage;
            ViewBag.CurrentAssigned = assigned;

            // The active state/quick filters narrow the cascading facets.
            await LoadFilterLists(status, availability, hasImage, assigned);
            // Page subtitle: the whole inventory, whatever the filters.
            ViewBag.Counts = await _mediator.Send(new GetProductCountsQuery());

            return View(products);
        }


        /// <summary>
        /// The toolbar's "find by code": an existing inventory code opens that product, anything
        /// else becomes a search on the product list.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Find(string? code)
        {
            code = code?.Trim();
            if (string.IsNullOrEmpty(code))
                return RedirectToAction(nameof(Index));

            if (int.TryParse(code, out var inventoryCode))
            {
                var product = await _mediator.Send(new GetProductByInventoryCodeQuery(inventoryCode));
                if (product != null)
                    return RedirectToAction(nameof(Details), new { id = product.Id });
            }
            return RedirectToAction(nameof(Index), new { search = code });
        }

        /// <summary>Suggestions under the toolbar's code search: up to six products matching the text.</summary>
        [HttpGet]
        public async Task<IActionResult> Suggest(string? term)
        {
            term = term?.Trim();
            if (string.IsNullOrEmpty(term))
                return Json(Array.Empty<object>());

            var result = await _mediator.Send(new GetAllProductsQuery(1, 6, term));
            var products = ModelMapper.Map<PagedResultDto<ProductViewModel>>(result).Items;
            // An exact code first, so Enter opens the product that was typed.
            return Json(products
                .OrderByDescending(p => p.InventoryCode.ToString() == term)
                .Select(p => new
                {
                    id = p.Id,
                    code = p.InventoryCode,
                    model = p.Model,
                    vendor = p.Vendor,
                    department = p.DepartmentName,
                    imageUrl = p.ImageUrl
                }));
        }

        public async Task<IActionResult> Details(int id)
        {
            var product = await _mediator.Send(new GetProductByIdQuery(id));
            if (product == null)
                return RedirectToNotFound();

            // The transfers below the details (the full history, updates included, is the timeline).
            var routes = await _mediator.Send(new GetRoutesByProductQuery(id));
            ViewBag.Transfers = ModelMapper.MapList<RouteViewModel>(routes)
                .Where(r => r.RouteTypeName == "Transfer")
                .OrderByDescending(r => r.CreatedAt)
                .Take(10)
                .ToList();
            var model = ModelMapper.Map<ProductViewModel>(product);
            var pending = await _mediator.Send(new ApprovalService.Application.Features.Queries.GetPendingProductRequests.Query());
            model.PendingRequestId = pending.TryGetValue(id, out var requestId) ? requestId : null;
            return View(model);
        }


        [PermissionAuthorize(AllPermissions.ProductCreate, AllPermissions.ProductCreateDirect)]
        public async Task<IActionResult> Create()
        {
            var model = new ProductViewModel();
            await LoadDropdowns(model);
            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AllPermissions.ProductCreate, AllPermissions.ProductCreateDirect)]
        public async Task<IActionResult> Create(ProductViewModel productModel)
        {
            if (!ModelState.IsValid)
            {
                await LoadDropdowns(productModel);
                return HandleValidationErrors(productModel);
            }

            var dto = new ModuleDtos.CreateProductDto
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
                DepartmentId = productModel.DepartmentId,
                ImageFiles = productModel.ImageFiles
            };

            var response = await RunAsync(
                () => _productManagement.CreateProductWithApprovalAsync(dto, GetCurrentUserId(), GetCurrentUserName(), GetCurrentUserPermissions()),
                "Product created successfully");
            return HandleApiResponse(response, nameof(Index));
        }


        [PermissionAuthorize(AllPermissions.ProductUpdate, AllPermissions.ProductUpdateDirect)]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _mediator.Send(new GetProductByIdQuery(id));
            if (product == null)
                return RedirectToNotFound();

            var model = ModelMapper.Map<ProductViewModel>(product);
            await LoadDropdowns(model);
            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AllPermissions.ProductUpdate, AllPermissions.ProductUpdateDirect)]
        public async Task<IActionResult> Edit(int id, ProductViewModel productModel)
        {
            if (!ModelState.IsValid)
            {
                await LoadDropdowns(productModel);
                return HandleValidationErrors(productModel);
            }

            var dto = new ModuleDtos.UpdateProductDto
            {
                Model = productModel.Model,
                Vendor = productModel.Vendor,
                Worker = productModel.Worker,
                Description = productModel.Description,
                CategoryId = productModel.CategoryId,
                DepartmentId = productModel.DepartmentId,
                IsWorking = productModel.IsWorking,
                IsActive = productModel.IsActive,
                IsNewItem = productModel.IsNewItem,
                ImageFiles = productModel.ImageFiles,
                RemoveImageUrls = productModel.RemoveImageUrls,
                CoverImageUrl = productModel.CoverImageUrl
            };

            var response = await RunAsync(
                () => _productManagement.UpdateProductWithApprovalAsync(id, dto, GetCurrentUserId(), GetCurrentUserName(), GetCurrentUserPermissions()),
                "Product updated successfully");
            return HandleApiResponse(response, nameof(Index));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AllPermissions.ProductUpdate, AllPermissions.ProductUpdateDirect)]
        public async Task<IActionResult> UpdateInventoryCode([FromBody] UpdateInventoryCodeDto request)
        {
            var response = await RunAsync(() => _mediator.Send(new UpdateProductInventoryCode.Command(request.Id, request.InventoryCode)));
            return response.IsSuccess
                ? Json(new { success = true })
                : BadRequest(new { error = response.Message });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AllPermissions.ProductDelete, AllPermissions.ProductDeleteDirect)]
        public async Task<IActionResult> Delete(int id)
        {
            var response = await RunAsync(
                () => _productManagement.DeleteProductWithApprovalAsync(id, GetCurrentUserId(), GetCurrentUserName(), GetCurrentUserPermissions()),
                "Product deleted successfully");
            return HandleApiResponse(response, nameof(Index));
        }


        /// <summary>
        /// Category/department options for the filter panel, plus the (department, category) pairs
        /// present in the inventory, emitted as a compact [[deptId, catId], ...] array that the
        /// filter JS turns into cascading lookups.
        /// </summary>
        private async Task LoadFilterLists(bool? status, bool? availability, bool? hasImage, bool? assigned)
        {
            var lookups = await _mediator.Send(new GetLookupsQuery());
            ViewBag.FilterCategories = lookups.Categories.ToSelectList();
            ViewBag.FilterDepartments = lookups.Departments.ToSelectList();

            var facets = await _mediator.Send(new GetProductFilterFacetsQuery(status, availability, hasImage, assigned));
            ViewBag.FilterPairsJson = System.Text.Json.JsonSerializer.Serialize(
                facets.Pairs.Select(p => new[] { p.DepartmentId, p.CategoryId }));
        }

        private async Task LoadDropdowns(ProductViewModel model)
        {
            var lookups = await _mediator.Send(new GetLookupsQuery());
            model.Categories = lookups.Categories.ToSelectList();
            model.Departments = lookups.Departments.ToSelectList();
        }
    }
}
