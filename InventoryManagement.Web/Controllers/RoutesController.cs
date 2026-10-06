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
using RouteService.Domain.Common;
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
            string[]? categoryName = null,
            string[]? routeType = null,
            string? departmentName = null,
            string? sort = null,
            string? dir = null,
            string? product = null,
            string[]? fromDepartment = null,
            string[]? toDepartment = null,
            string[]? whatsApp = null,
            CancellationToken cancellationToken = default)
        {
            // The filter bar sends one value and the column headers several, so both arrive as lists
            var types = (routeType ?? []).Select(ParseRouteType).OfType<RouteType>().Distinct().ToArray();
            var filter = new RouteListFilter
            {
                Sort = sort,
                Descending = dir == "desc",
                Product = product,
                FromDepartments = NonEmpty(fromDepartment),
                ToDepartments = NonEmpty(toDepartment),
                Categories = NonEmpty(categoryName),
                RouteTypes = types,
                WhatsApp = NonEmpty(whatsApp)
            };

            ViewBag.CurrentFilter = isCompleted;
            ViewBag.StartDate = startDate;
            ViewBag.CurrentSearch = search;
            ViewBag.EndDate = endDate;
            ViewBag.CurrentDepartmentName = departmentName;

            try
            {
                // Both tab counts come from one grouped query, which also gives the list its total
                var counts = await _mediator.Send(
                    new GetRouteCountsQuery(search, startDate, endDate, departmentId, departmentName, filter), cancellationToken);
                ViewBag.PendingCount = counts.Pending;
                ViewBag.CompletedCount = counts.Completed;
                var total = isCompleted switch
                {
                    true => counts.Completed,
                    false => counts.Pending,
                    null => counts.Pending + counts.Completed
                };

                // The department filter matches either end of a route, and routes store only the category name
                var result = await _mediator.Send(new GetAllRoutesQuery(
                    pageNumber, pageSize, search, isCompleted, startDate, endDate,
                    departmentId, null, null, departmentName, filter, total), cancellationToken);
                var routes = ModelMapper.Map<PagedResultDto<RouteViewModel>>(result);
                foreach (var r in routes.Items)
                    r.Localize();

                await LoadFilterLists(isCompleted, types.Length == 1 ? types[0] : null, cancellationToken);

                return View(routes);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A newer search replaced this one in the browser, so nobody reads the answer
                return new EmptyResult();
            }
        }

        private static string[]? NonEmpty(string[]? values)
        {
            var kept = values?.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToArray();
            return kept is { Length: > 0 } ? kept : null;
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

            // A completed route cannot change, so its page is the details page
            if (route.IsCompleted)
            {
                TempData["Error"] = "Completed routes cannot be edited";
                return RedirectToAction(nameof(Details), new { id });
            }

            ViewBag.Departments = await GetDepartmentOptions(route.ToDepartmentId);
            // The notes go into an input here, so only the placeholder words are translated
            return View(ModelMapper.Map<RouteViewModel>(route).LocalizePlaceholders());
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
                // The form always posts both, and an emptied field arrives as null but means cleared
                ToWorker = model.ToWorker ?? string.Empty,
                Notes = model.Notes ?? string.Empty
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
            return View(ModelMapper.MapList<RouteViewModel>(routes).Select(r => r.Localize()).ToList());
        }


        [PermissionAuthorize(AllPermissions.RouteView)]
        public async Task<IActionResult> Details(int id)
        {
            var route = await _mediator.Send(new GetRouteByIdQuery(id));
            return route == null
                ? RedirectToNotFound()
                : View(ModelMapper.Map<RouteViewModel>(route).Localize());
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AllPermissions.RouteComplete)]
        public async Task<IActionResult> Complete(int id)
        {
            var response = await RunAsync(() => _mediator.Send(new CompleteRoute.Command(id)), "Route completed successfully");
            return HandleApiResponse(response, nameof(Index));
        }

        /// <summary>Queues a failed WhatsApp message of a completed transfer again.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AllPermissions.RouteComplete)]
        public async Task<IActionResult> ResendWhatsApp(int id,
            [FromServices] SharedServices.Contracts.IWhatsAppRouteNotifier whatsApp,
            [FromServices] SharedServices.Contracts.IRouteWhatsAppStatus status)
        {
            var route = await _mediator.Send(new GetRouteByIdQuery(id));
            if (route == null || !route.IsCompleted || route.RouteType != RouteService.Domain.Enums.RouteType.Transfer)
                return Json(new { isSuccess = false, message = Tr("WhatsApp messages are sent only for completed transfers.") });
            if (!whatsApp.Enabled)
                return Json(new { isSuccess = false, message = Tr("WhatsApp is not configured.") });
            // Failed messages only, since resending others could flood the group and WaSender bans accounts for that
            if (route.WhatsAppStatus != SharedServices.Contracts.WhatsAppStatus.Failed)
                return Json(new { isSuccess = false, message = Tr("Only a message that failed to send can be resent.") });

            await status.SetAsync(id, SharedServices.Contracts.WhatsAppStatus.Queued, null);
            await whatsApp.QueueRouteCompletedAsync(new SharedServices.Events.RouteCompletedEvent
            {
                RouteId = route.Id,
                ProductId = route.ProductId,
                InventoryCode = route.InventoryCode,
                Model = route.Model,
                Vendor = route.Vendor,
                CategoryName = route.CategoryName,
                FromDepartmentName = route.FromDepartmentName ?? string.Empty,
                FromWorker = route.FromWorker,
                ToDepartmentName = route.ToDepartmentName,
                ToWorker = route.ToWorker,
                Notes = route.Notes,
                ImageUrl = route.ImageUrl,
                CompletedAt = route.CompletedAt ?? DateTime.Now
            });
            return Json(new { isSuccess = true, message = Tr("The WhatsApp message is queued and will be sent in a few seconds.") });
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

        /// <summary>Active departments, plus the current one even if it is inactive.</summary>
        private async Task<List<SelectListItem>> GetDepartmentOptions(int? currentId = null)
            => (await _mediator.Send(new GetLookupsQuery())).Departments.ToChoiceList(currentId);

        private async Task LoadDepartments(TransferViewModel model)
            => model.Departments = await GetDepartmentOptions();

        /// <summary>Filter options come from the names stored on routes, so renamed and deleted departments still show up.</summary>
        private async Task LoadFilterLists(bool? isCompleted, RouteType? routeType, CancellationToken cancellationToken)
        {
            var facets = await _mediator.Send(new GetRouteFilterFacetsQuery(isCompleted, routeType), cancellationToken);
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
