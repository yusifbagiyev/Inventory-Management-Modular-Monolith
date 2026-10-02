using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RouteService.Application.DTOs;
using RouteService.Application.Features.Routes.Commands;
using RouteService.Application.Features.Routes.Queries;
using RouteService.Application.Interfaces;
using RouteService.Domain.Enums;
using SharedServices.Authorization;
using SharedServices.Identity;

namespace RouteService.API.Controllers
{
    /// <remarks>Module exceptions are turned into status codes by the host's API exception middleware.</remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class InventoryRoutesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IRouteManagementService _routeManagementService;

        public InventoryRoutesController(IMediator mediator, IRouteManagementService routeManagementService)
        {
            _mediator = mediator;
            _routeManagementService = routeManagementService;
        }


        /// <summary>Transfers directly with route.create.direct, otherwise queues it for approval and answers 202.</summary>
        [HttpPost("transfer")]
        [Consumes("multipart/form-data")]
        [Permission(AllPermissions.RouteCreate, AllPermissions.RouteCreateDirect)]
        public async Task<IActionResult> TransferInventory([FromForm] TransferInventoryDto dto)
        {
            var result = await _routeManagementService.TransferInventoryWithApprovalAsync(
                dto,
                _routeManagementService.GetUserId(User),
                _routeManagementService.GetUserName(User),
                _routeManagementService.GetUserPermissions(User));
            return Ok(result);
        }


        [HttpPut("{id}")]
        [Consumes("multipart/form-data")]
        [Permission(AllPermissions.RouteUpdate, AllPermissions.RouteUpdateDirect)]
        public async Task<IActionResult> UpdateRoute(int id, [FromForm] UpdateRouteDto dto)
        {
            await _routeManagementService.UpdateRouteWithApprovalAsync(
                id,
                dto,
                _routeManagementService.GetUserId(User),
                _routeManagementService.GetUserName(User),
                _routeManagementService.GetUserPermissions(User));
            return NoContent();
        }


        // The Transfer page uses this too, to check for a pending transfer of the product.
        [HttpGet("product/{productId}")]
        [Permission(AllPermissions.RouteView, AllPermissions.RouteCreate, AllPermissions.RouteCreateDirect)]
        public async Task<ActionResult<IEnumerable<InventoryRouteDto>>> GetInventoryByProductId(int productId)
        {
            var result = await _mediator.Send(new GetRoutesByProductQuery(productId));
            return Ok(result);
        }


        [HttpGet]
        [Permission(AllPermissions.RouteView)]
        public async Task<ActionResult<PagedResultDto<InventoryRouteDto>>> GetAllRoutes(
            [FromQuery] int? pageNumber = 1,
            [FromQuery] int? pageSize = 30,
            [FromQuery] string? search = null,
            [FromQuery] bool? isCompleted = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int? departmentId = null,
            [FromQuery] string? categoryName = null,
            [FromQuery] RouteType? routeType = null)
        {
            var result = await _mediator.Send(new GetAllRoutesQuery(
                pageNumber, pageSize, search, isCompleted, startDate, endDate,
                departmentId, categoryName, routeType));
            return Ok(result);
        }


        /// <summary>Department and category name pairs found on routes, so the list filters can cascade.</summary>
        [HttpGet("filter-facets")]
        [Permission(AllPermissions.RouteView)]
        public async Task<ActionResult<RouteFilterFacetsDto>> GetFilterFacets(
            [FromQuery] bool? isCompleted = null,
            [FromQuery] RouteType? routeType = null)
        {
            var facets = await _mediator.Send(new GetRouteFilterFacetsQuery(isCompleted, routeType));
            return Ok(facets);
        }


        [HttpGet("{id}")]
        [Permission(AllPermissions.RouteView)]
        public async Task<ActionResult<InventoryRouteDto>> GetById(int id)
        {
            var result = await _mediator.Send(new GetRouteByIdQuery(id));
            return result == null ? NotFound() : Ok(result);
        }


        [HttpPut("{id}/complete")]
        [Permission(AllPermissions.RouteComplete)]
        public async Task<IActionResult> CompleteRoute(int id)
        {
            await _mediator.Send(new CompleteRoute.Command(id));
            return NoContent();
        }


        [HttpDelete("{id}")]
        [Permission(AllPermissions.RouteDelete, AllPermissions.RouteDeleteDirect)]
        public async Task<IActionResult> DeleteRoute(int id)
        {
            await _routeManagementService.DeleteRouteWithApprovalAsync(
                id,
                _routeManagementService.GetUserId(User),
                _routeManagementService.GetUserName(User),
                _routeManagementService.GetUserPermissions(User));
            return NoContent();
        }
    }
}
