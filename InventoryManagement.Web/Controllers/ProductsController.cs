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

            return View(products);
        }


        public async Task<IActionResult> Details(int id)
        {
            var product = await _mediator.Send(new GetProductByIdQuery(id));
            return product == null
                ? RedirectToNotFound()
                : View(ModelMapper.Map<ProductViewModel>(product));
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
