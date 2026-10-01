using InventoryManagement.Web.Extensions;
using InventoryManagement.Web.Filters;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using ProductService.Application.Features.Lookups;
using RouteService.Application.Features.Routes.Commands;
using RouteService.Application.Features.Routes.Queries;
using RouteService.Application.Interfaces;
using RouteService.Domain.Enums;
using SharedServices.Identity;
using ModuleDtos = RouteService.Application.DTOs;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
    public class RoutesController : BaseController
    {
        private readonly IMediator _mediator;
        private readonly IRouteManagementService _routeManagement;

        public RoutesController(
            IMediator mediator,
            IRouteManagementService routeManagement,
            ILogger<RoutesController> logger)
            : base(logger)
        {
            _mediator = mediator;
            _routeManagement = routeManagement;
        }

        [PermissionAuthorize(AllPermissions.RouteView)]
        public async Task<IActionResult> Index(
            int? pageNumber = 1,
            int? pageSize = 30,
            string? search = null,
            bool? isCompleted = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int? departmentId = null,
            string? categoryName = null,
            string? routeType = null,
            string? departmentName = null)
        {
            var type = ParseRouteType(routeType);

            // Department matches either end of a route; category is matched by name (routes store
            // only the category name).
            var result = await _mediator.Send(new GetAllRoutesQuery(
                pageNumber, pageSize, search, isCompleted, startDate, endDate,
                departmentId, categoryName, type, departmentName));
            var routes = ModelMapper.Map<PagedResultDto<RouteViewModel>>(result);
            foreach (var r in routes.Items)
                TranslateNotes(r);


            ViewBag.CurrentFilter = isCompleted;
            ViewBag.StartDate = startDate;
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStatus = isCompleted;
            ViewBag.EndDate = endDate;
            ViewBag.PageNumber = pageNumber ?? 1;
            ViewBag.PageSize = pageSize ?? 30;
            ViewBag.CurrentDepartmentId = departmentId;
            ViewBag.CurrentDepartmentName = departmentName;
            ViewBag.CurrentCategoryName = categoryName;
            ViewBag.CurrentRouteType = routeType;

            // The active status/type filters narrow the cascading facets.
            await LoadFilterLists(isCompleted, type);

            // Tab counts (All / Pending / Completed): the same filters with each completion state.
            ViewBag.PendingCount = isCompleted == false ? routes.TotalCount
                : (await _mediator.Send(new GetAllRoutesQuery(1, 1, search, false, startDate, endDate, departmentId, categoryName, type, departmentName))).TotalCount;
            ViewBag.CompletedCount = isCompleted == true ? routes.TotalCount
                : (await _mediator.Send(new GetAllRoutesQuery(1, 1, search, true, startDate, endDate, departmentId, categoryName, type, departmentName))).TotalCount;

            return View(routes);
        }


        [PermissionAuthorize(AllPermissions.RouteCreate, AllPermissions.RouteCreateDirect)]
        public async Task<IActionResult> Transfer()
        {
            var model = new TransferViewModel();
            await LoadDepartments(model);
            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AllPermissions.RouteCreate, AllPermissions.RouteCreateDirect)]
        public async Task<IActionResult> Transfer(TransferViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadDepartments(model);
                return HandleValidationErrors(model);
            }

            var dto = new ModuleDtos.TransferInventoryDto
            {
                ProductId = model.ProductId,
                ToDepartmentId = model.ToDepartmentId,
                ToWorker = model.ToWorker,
                Notes = model.Notes,
                ImageFiles = model.ImageFiles
            };

            var response = await RunAsync(
                () => _routeManagement.TransferInventoryWithApprovalAsync(dto, GetCurrentUserId(), GetCurrentUserName(), GetCurrentUserPermissions()),
                "Transfer created successfully");
            return HandleApiResponse(response, nameof(Index));
        }


        [HttpGet]
        [PermissionAuthorize(AllPermissions.RouteUpdate, AllPermissions.RouteUpdateDirect)]
        public async Task<IActionResult> Edit(int id)
        {
            var route = await _mediator.Send(new GetRouteByIdQuery(id));
            if (route == null)
                return RedirectToNotFound();

            // Needed for the destination dropdown.
            ViewBag.Departments = await GetDepartmentOptions(route.ToDepartmentId);
            return View(ModelMapper.Map<RouteViewModel>(route));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AllPermissions.RouteUpdate, AllPermissions.RouteUpdateDirect)]
        public async Task<IActionResult> Edit(int id, [FromForm] UpdateRouteViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Departments = await GetDepartmentOptions(model.ToDepartmentId);
                return HandleValidationErrors(model);
            }

            var dto = new ModuleDtos.UpdateRouteDto
            {
                ImageFiles = model.ImageFiles,
                RemoveImageUrls = model.RemoveImageUrls,
                CoverImageUrl = model.CoverImageUrl,
                ToDepartmentId = model.ToDepartmentId,
                ToWorker = model.ToWorker,
                Notes = model.Notes
            };

            var response = await RunAsync(
                () => _routeManagement.UpdateRouteWithApprovalAsync(id, dto, GetCurrentUserId(), GetCurrentUserName(), GetCurrentUserPermissions()),
                "Route updated successfully");
            return HandleApiResponse(response, nameof(Index));
        }


        [PermissionAuthorize(AllPermissions.RouteView)]
        public async Task<IActionResult> Timeline(int productId)
        {
            var routes = await _mediator.Send(new GetRoutesByProductQuery(productId));
            ViewBag.ProductId = productId;
            return View(ModelMapper.MapList<RouteViewModel>(routes).Select(TranslateNotes).ToList());
        }


        [PermissionAuthorize(AllPermissions.RouteView)]
        public async Task<IActionResult> Details(int id)
        {
            var route = await _mediator.Send(new GetRouteByIdQuery(id));
            return route == null
                ? RedirectToNotFound()
                : View(TranslateNotes(ModelMapper.Map<RouteViewModel>(route)));
        }

        /// <summary>
        /// History rows carry notes the system wrote in English ("Auto-created from product service",
        /// "Product updated: …"); show them in the interface language. User-typed notes match no key
        /// and stay as written.
        /// </summary>
        private static RouteViewModel TranslateNotes(RouteViewModel route)
        {
            route.Notes = Tr(route.Notes);
            return route;
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AllPermissions.RouteComplete)]
        public async Task<IActionResult> Complete(int id)
        {
            var response = await RunAsync(() => _mediator.Send(new CompleteRoute.Command(id)), "Route completed successfully");
            return HandleApiResponse(response, nameof(Index));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AllPermissions.RouteDelete, AllPermissions.RouteDeleteDirect)]
        public async Task<IActionResult> Delete(int id)
        {
            var response = await RunAsync(
                () => _routeManagement.DeleteRouteWithApprovalAsync(id, GetCurrentUserId(), GetCurrentUserName(), GetCurrentUserPermissions()),
                "Route deleted successfully");
            return HandleApiResponse(response, nameof(Index));
        }


        private static RouteType? ParseRouteType(string? routeType)
            => Enum.TryParse<RouteType>(routeType, ignoreCase: true, out var parsed) ? parsed : null;

        /// <summary>Active departments (plus <paramref name="currentId"/> when a route already points at an inactive one).</summary>
        private async Task<List<SelectListItem>> GetDepartmentOptions(int? currentId = null)
            => (await _mediator.Send(new GetLookupsQuery())).Departments.ToChoiceList(currentId);

        private async Task LoadDepartments(TransferViewModel model)
            => model.Departments = await GetDepartmentOptions();

        /// <summary>
        /// Department/category options + cascading data for the route list's filter panel. A route
        /// keeps the department and category NAMES of the moment it was written, so both dropdowns
        /// offer the names found on routes (renamed and deleted departments included) and filter by
        /// them. Pairs are emitted as [["department", "category"], ...].
        /// </summary>
        private async Task LoadFilterLists(bool? isCompleted, RouteType? routeType)
        {
            var facets = await _mediator.Send(new GetRouteFilterFacetsQuery(isCompleted, routeType));
            static List<SelectListItem> Options(IEnumerable<string> names) => names
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
                .Select(n => new SelectListItem { Value = n, Text = n })
                .ToList();

            ViewBag.FilterCategories = Options(facets.Pairs.Select(p => p.CategoryName));
            ViewBag.FilterDepartments = Options(facets.Pairs.Select(p => p.DepartmentName));
            ViewBag.FilterPairsJson = System.Text.Json.JsonSerializer.Serialize(
                facets.Pairs.Select(p => new object[] { p.DepartmentName, p.CategoryName }));
        }
    }
}
