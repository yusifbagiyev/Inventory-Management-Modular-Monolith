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
    [Authorize(Roles = AllRoles.Admin)]
    public class DepartmentsController : BaseController
    {
        /// <summary>Upper bound for the product table on the details page and the Word export.</summary>
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

        public async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 20, string? search = null)
        {
            var page = await _mediator.Send(new GetPagedDepartmentsQuery(pageNumber, pageSize, search));
            var stats = await _mediator.Send(new GetDepartmentStatsQuery());

            ViewBag.ActiveDepartments = stats.Active;
            ViewBag.InActiveDepartments = stats.Inactive;
            ViewBag.DepartmentsInWithProducts = stats.Products;
            ViewBag.CurrentSearch = search;
            ViewBag.PageNumber = pageNumber;
            ViewBag.PageSize = pageSize;

            return View(ModelMapper.Map<PagedResultDto<DepartmentViewModel>>(page));
        }


        public async Task<IActionResult> Details(int id)
        {
            var department = await _mediator.Send(new GetDepartmentByIdQuery(id));
            if (department == null)
                return RedirectToNotFound();

            var products = await GetDepartmentProducts(id);
            var model = ModelMapper.Map<DepartmentViewModel>(department);
            model.ProductCount = products.Count;
            model.WorkerCount = products
                .Where(p => !string.IsNullOrEmpty(p.Worker))
                .Select(p => p.Worker)
                .Distinct()
                .Count();
            ViewBag.Products = products;

            return View(model);
        }


        public IActionResult Create() => View(new DepartmentViewModel());


        [HttpPost]
        [ValidateAntiForgeryToken]
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


        public async Task<IActionResult> Edit(int id)
        {
            var department = await _mediator.Send(new GetDepartmentByIdQuery(id));
            return department == null
                ? RedirectToNotFound()
                : View(ModelMapper.Map<DepartmentViewModel>(department));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
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
            var response = await RunAsync(() => _mediator.Send(new UpdateDepartment.Command(id, dto)), "Department updated successfully");
            return HandleApiResponse(response, nameof(Index));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var response = await RunAsync(() => _mediator.Send(new DeleteDepartment.Command(id)), "Department deleted successfully");
            return HandleApiResponse(response, nameof(Index));
        }


        /// <summary>Department inventory as a Word document ("Təhvil verdi" is the exporting user).</summary>
        public async Task<IActionResult> ExportToWord(int id)
        {
            var department = await _mediator.Send(new GetDepartmentByIdQuery(id));
            if (department == null)
                return RedirectToNotFound();

            var products = await GetDepartmentProducts(id);
            var exportedByFullName = $"{User.FindFirst("FirstName")?.Value} {User.FindFirst("LastName")?.Value}".Trim();

            var fileBytes = _wordExportService.GenerateDepartmentInventoryDocument(
                ModelMapper.Map<DepartmentViewModel>(department), products, exportedByFullName);

            return File(fileBytes,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                $"{department.Name}_Inventory_{DateTime.Now:yyyyMMdd}.docx");
        }


        private async Task<List<ProductViewModel>> GetDepartmentProducts(int departmentId)
        {
            var products = await _mediator.Send(new GetAllProductsQuery(1, MaxProductsListed, departmentId: departmentId));
            return ModelMapper.MapList<ProductViewModel>(products.Items);
        }
    }
}
