using InventoryManagement.Web.Filters;
using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductService.Application.Features.Categories.Commands;
using ProductService.Application.Features.Categories.Queries;
using ProductService.Application.Features.Lookups;
using ProductService.Application.Features.Products.Queries;
using SharedServices.Identity;
using ModuleDtos = ProductService.Application.DTOs;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
    public class CategoriesController : BaseController
    {
        /// <summary>Upper bound for the product table on the details page.</summary>
        private const int MaxProductsListed = 10000;

        private readonly IMediator _mediator;

        public CategoriesController(IMediator mediator, ILogger<CategoriesController> logger)
            : base(logger)
        {
            _mediator = mediator;
        }

        [PermissionAuthorize(AllPermissions.CategoryView)]
        public async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 20, string? search = null)
        {
            var page = await _mediator.Send(new GetPagedCategoriesQuery(pageNumber, pageSize, search));
            var stats = await _mediator.Send(new GetCategoryStatsQuery());

            ViewBag.ActiveCategories = stats.Active;
            ViewBag.InActiveCategories = stats.Inactive;
            ViewBag.CategoriesInWithProducts = stats.Products;
            ViewBag.CurrentSearch = search;
            ViewBag.PageNumber = pageNumber;
            ViewBag.PageSize = pageSize;

            return View(ModelMapper.Map<PagedResultDto<CategoryViewModel>>(page));
        }


        [PermissionAuthorize(AllPermissions.CategoryView)]
        public async Task<IActionResult> Details(int id)
        {
            var category = await _mediator.Send(new GetCategoryByIdQuery(id));
            if (category == null)
                return RedirectToNotFound();

            var products = await _mediator.Send(new GetAllProductsQuery(1, MaxProductsListed, categoryId: id));
            var model = ModelMapper.Map<CategoryViewModel>(category);
            model.ProductCount = products.TotalCount;
            ViewBag.Products = ModelMapper.MapList<ProductViewModel>(products.Items);

            return View(model);
        }


        [PermissionAuthorize(AllPermissions.CategoryCreate)]
        public IActionResult Create() => View(new CategoryViewModel());


        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AllPermissions.CategoryCreate)]
        public async Task<IActionResult> Create(CategoryViewModel model)
        {
            if (!ModelState.IsValid)
                return HandleValidationErrors(model);

            var dto = new ModuleDtos.CreateCategoryDto
            {
                Name = model.Name,
                Description = model.Description,
                IsActive = model.IsActive
            };
            var response = await RunAsync(() => _mediator.Send(new CreateCategory.Command(dto)), "Category created successfully");
            return HandleApiResponse(response, nameof(Index));
        }


        [PermissionAuthorize(AllPermissions.CategoryUpdate)]
        public async Task<IActionResult> Edit(int id)
        {
            var category = await _mediator.Send(new GetCategoryByIdQuery(id));
            return category == null
                ? RedirectToNotFound()
                : View(ModelMapper.Map<CategoryViewModel>(category));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AllPermissions.CategoryUpdate)]
        public async Task<IActionResult> Edit(int id, CategoryViewModel model)
        {
            if (!ModelState.IsValid)
                return HandleValidationErrors(model);

            var dto = new ModuleDtos.UpdateCategoryDto
            {
                Name = model.Name,
                Description = model.Description,
                IsActive = model.IsActive
            };
            var response = await RunAsync(() => _mediator.Send(new UpdateCategory.Command(id, dto)), "Category updated successfully");
            return HandleApiResponse(response, nameof(Index));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AllPermissions.CategoryDelete)]
        public async Task<IActionResult> Delete(int id)
        {
            var response = await RunAsync(() => _mediator.Send(new DeleteCategory.Command(id)), "Category deleted successfully");
            return HandleApiResponse(response, nameof(Index));
        }
    }
}
