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

        [PermissionAuthorize(AllPermissions.ProductView)]
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

            await LoadFilterLists(status, availability, hasImage, assigned);
            // The subtitle counts the whole inventory and ignores the filters
            ViewBag.Counts = await _mediator.Send(new GetProductCountsQuery());

            return View(products);
        }


        /// <summary>Opens the product for a known inventory code, otherwise searches the list.</summary>
        [HttpGet]
        [PermissionAuthorize(AllPermissions.ProductView)]
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

        /// <summary>Up to six suggestions for the toolbar's code search.</summary>
        [HttpGet]
        [PermissionAuthorize(AllPermissions.ProductView)]
        public async Task<IActionResult> Suggest(string? term)
        {
            term = term?.Trim();
            if (string.IsNullOrEmpty(term))
                return Json(Array.Empty<object>());

            var result = await _mediator.Send(new GetAllProductsQuery(1, 6, term));
            var products = ModelMapper.Map<PagedResultDto<ProductViewModel>>(result).Items;
            // Exact code match first so Enter opens the product that was typed
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

        /// <summary>Lists soft-deleted products, or shows one when an id is given.</summary>
        [PermissionAuthorize(AllPermissions.ProductDeletedView)]
        public async Task<IActionResult> Deleted(int? id, string? search = null, int pageNumber = 1, int pageSize = 30)
        {
            if (id.HasValue)
            {
                var product = await _mediator.Send(new GetDeletedProductByIdQuery(id.Value));
                return product == null ? RedirectToNotFound() : View("DeletedDetails", ModelMapper.Map<ProductViewModel>(product));
            }

            var page = await _mediator.Send(new GetDeletedProductsQuery(search, pageNumber, pageSize));
            ViewBag.TotalCount = page.TotalCount;
            ViewBag.PageNumber = page.PageNumber;
            ViewBag.PageSize = page.PageSize;
            return View(ModelMapper.MapList<ProductViewModel>(page.Items));
        }

        [PermissionAuthorize(AllPermissions.ProductView)]
        public async Task<IActionResult> Details(int id)
        {
            var product = await _mediator.Send(new GetProductByIdQuery(id));
            if (product == null)
            {
                // Old links to a product deleted since then go to its kept record
                if (User.HasPermission(AllPermissions.ProductDeletedView)
                    && await _mediator.Send(new GetDeletedProductByIdQuery(id)) != null)
                    return RedirectToAction(nameof(Deleted), new { id });
                return RedirectToNotFound();
            }

            // Transfers only, the full history with updates is on the timeline page
            var routes = User.HasPermission(AllPermissions.RouteView)
                ? await _mediator.Send(new GetRoutesByProductQuery(id))
                : [];
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
                Color = productModel.Color,
                Specifications = ToSpecificationDtos(productModel.Specifications),
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
                CoverImageUrl = productModel.CoverImageUrl,
                // The form always sends colour and specifications, so an empty list clears them
                ReplaceDetails = true,
                Color = productModel.Color,
                Specifications = ToSpecificationDtos(productModel.Specifications)
            };

            var response = await RunAsync(
                () => _productManagement.UpdateProductWithApprovalAsync(id, dto, GetCurrentUserId(), GetCurrentUserName(), GetCurrentUserPermissions()),
                "Product updated successfully");
            return HandleApiResponse(response, nameof(Index));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AllPermissions.ProductCodeUpdate)]
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


        /// <summary>Filter options plus the department and category pairs the filter script uses to cascade.</summary>
        private async Task LoadFilterLists(bool? status, bool? availability, bool? hasImage, bool? assigned)
        {
            var lookups = await _mediator.Send(new GetLookupsQuery());
            ViewBag.FilterCategories = lookups.Categories.ToSelectList();
            ViewBag.FilterDepartments = lookups.Departments.ToSelectList();

            var facets = await _mediator.Send(new GetProductFilterFacetsQuery(status, availability, hasImage, assigned));
            ViewBag.FilterPairsJson = System.Text.Json.JsonSerializer.Serialize(
                facets.Pairs.Select(p => new[] { p.DepartmentId, p.CategoryId }));
        }

        private static List<ModuleDtos.ProductSpecificationDto> ToSpecificationDtos(IEnumerable<ProductSpecificationViewModel>? lines)
            => (lines ?? [])
                .Where(l => !string.IsNullOrWhiteSpace(l.Name))
                .Select(l => new ModuleDtos.ProductSpecificationDto { Name = l.Name, Value = l.Value })
                .ToList();

        private async Task LoadDropdowns(ProductViewModel model)
        {
            var lookups = await _mediator.Send(new GetLookupsQuery());
            model.Categories = lookups.Categories.ToSelectList();
            // Keeps the product's current department even if it is inactive
            model.Departments = lookups.Departments.ToChoiceList(model.DepartmentId);
        }
    }
}
