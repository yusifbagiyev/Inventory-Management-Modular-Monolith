using InventoryManagement.Web.Filters;
using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services;
using InventoryManagement.Web.Services.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductService.Application.Features.Departments.Commands;
using ProductService.Application.Features.Departments.Queries;
using ProductService.Application.Features.Lookups;
using ProductService.Application.Features.Products.Queries;
using SharedServices.Identity;
using ModuleDtos = ProductService.Application.DTOs;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
    public class DepartmentsController : BaseController
    {
        // Cap for the details page table and the Word export
        private const int MaxProductsListed = 10000;

        private readonly IMediator _mediator;
        private readonly IWordExportService _wordExportService;

        public DepartmentsController(
            IMediator mediator,
            IWordExportService wordExportService,
            ILogger<DepartmentsController> logger)
            : base(logger)
        {
            _mediator = mediator;
            _wordExportService = wordExportService;
        }

        [PermissionAuthorize(AllPermissions.DepartmentView)]
        public async Task<IActionResult> Index(
            int pageNumber = 1, int pageSize = 30, string? search = null, string? sort = null, string? dir = null,
            string? name = null, string? head = null, string? description = null, bool[]? active = null,
            int? productsMin = null, int? productsMax = null, int? workersMin = null, int? workersMax = null,
            DateTime? createdFrom = null, DateTime? createdTo = null)
        {
            var filter = new ModuleDtos.CatalogListFilter
            {
                Sort = sort, Descending = dir == "desc", Name = name, Head = head, Description = description, Active = active,
                ProductsMin = productsMin, ProductsMax = productsMax, WorkersMin = workersMin, WorkersMax = workersMax,
                CreatedFrom = createdFrom, CreatedTo = createdTo
            };
            var page = await _mediator.Send(new GetPagedDepartmentsQuery(pageNumber, pageSize, search, filter));
            var stats = await _mediator.Send(new GetDepartmentStatsQuery());

            ViewBag.ActiveDepartments = stats.Active;
            ViewBag.InActiveDepartments = stats.Inactive;
            ViewBag.DepartmentsInWithProducts = stats.WithProducts;
            ViewBag.CurrentSearch = search;
            ViewBag.PageNumber = pageNumber;
            ViewBag.PageSize = pageSize;

            return View(ModelMapper.Map<PagedResultDto<DepartmentViewModel>>(page));
        }


        [PermissionAuthorize(AllPermissions.DepartmentView)]
        public async Task<IActionResult> Details(int id)
        {
            var department = await _mediator.Send(new GetDepartmentByIdQuery(id));
            if (department == null)
                return RedirectToNotFound();

            // The product and worker counts come with the department, counted the same way as on the list
            ViewBag.Products = await GetDepartmentProducts(id);

            return View(ModelMapper.Map<DepartmentViewModel>(department));
        }


        [PermissionAuthorize(AllPermissions.DepartmentCreate)]
        public IActionResult Create() => View(new DepartmentViewModel());


        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AllPermissions.DepartmentCreate)]
        public async Task<IActionResult> Create(DepartmentViewModel model)
        {
            if (!ModelState.IsValid)
                return HandleValidationErrors(model);

            var dto = new ModuleDtos.CreateDepartmentDto
            {
                Name = model.Name,
                DepartmentHead = model.DepartmentHead,
                Description = model.Description,
                IsActive = model.IsActive
            };
            var response = await RunAsync(() => _mediator.Send(new CreateDepartment.Command(dto)), "Department created successfully");
            return HandleApiResponse(response, nameof(Index));
        }


        [PermissionAuthorize(AllPermissions.DepartmentUpdate)]
        public async Task<IActionResult> Edit(int id)
        {
            var department = await _mediator.Send(new GetDepartmentByIdQuery(id));
            return department == null
                ? RedirectToNotFound()
                : View(ModelMapper.Map<DepartmentViewModel>(department));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AllPermissions.DepartmentUpdate)]
        public async Task<IActionResult> Edit(int id, DepartmentViewModel model)
        {
            if (!ModelState.IsValid)
                return HandleValidationErrors(model);

            var dto = new ModuleDtos.UpdateDepartmentDto
            {
                Name = model.Name,
                DepartmentHead = model.DepartmentHead,
                Description = model.Description,
                IsActive = model.IsActive
            };
            // The form posts every field, so an emptied one is meant to be cleared
            var response = await RunAsync(() => _mediator.Send(new UpdateDepartment.Command(id, dto, ClearBlankFields: true)), "Department updated successfully");
            return HandleApiResponse(response, nameof(Index));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AllPermissions.DepartmentDelete)]
        public async Task<IActionResult> Delete(int id)
        {
            var response = await RunAsync(() => _mediator.Send(new DeleteDepartment.Command(id)), "Department deleted successfully");
            return HandleApiResponse(response, nameof(Index));
        }


        /// <summary>Word hand-over document, signed off by the exporting user.</summary>
        [PermissionAuthorize(AllPermissions.DepartmentExport)]
        public async Task<IActionResult> ExportToWord(int id)
        {
            var department = await _mediator.Send(new GetDepartmentByIdQuery(id));
            if (department == null)
                return RedirectToNotFound();

            // The hand-over lists only what is in use, so deactivated items are left out
            var products = await GetDepartmentProducts(id, activeOnly: true);
            var exportedByFullName = $"{User.FindFirst("FirstName")?.Value} {User.FindFirst("LastName")?.Value}".Trim();

            var fileBytes = _wordExportService.GenerateDepartmentInventoryDocument(
                ModelMapper.Map<DepartmentViewModel>(department), products, exportedByFullName);

            return File(fileBytes,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                $"{department.Name}_Inventory_{DateTime.Now:yyyyMMdd}.docx");
        }


        private async Task<List<ProductViewModel>> GetDepartmentProducts(int departmentId, bool activeOnly = false)
        {
            var products = await _mediator.Send(new GetAllProductsQuery(1, MaxProductsListed,
                departmentId: departmentId, availability: activeOnly ? true : null));
            return ModelMapper.MapList<ProductViewModel>(products.Items);
        }
    }
}
