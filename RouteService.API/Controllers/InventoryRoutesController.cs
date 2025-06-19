using IdentityService.Domain.Constants;
using IdentityService.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RouteService.Application.DTOs;
using RouteService.Application.Features.Routes.Commands;
using RouteService.Application.Features.Routes.Queries;
using RouteService.Domain.Common;

namespace RouteService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class InventoryRoutesController : ControllerBase
    {
        private readonly IMediator _mediator;
        public InventoryRoutesController(IMediator mediator)
        {
            _mediator = mediator;
        }
        [HttpPost("transfer")]
        [Consumes("multipart/form-data")]
        [Permission(AllPermissions.RouteCreate)]
        public async Task<ActionResult<InventoryRouteDto>> TransferInventory([FromForm] TransferInventoryDto dto)
        {
            var result = await _mediator.Send(new TransferInventory.Command(dto));
            return Ok(result);
        }


        [HttpGet("product/{productId}")]
        [Permission(AllPermissions.RouteView)]
        public async Task<ActionResult<IEnumerable<InventoryRouteDto>>> GetInventoryByProductId(int productId)
        {
            var result = await _mediator.Send(new GetRoutesByProductQuery(productId));
            return Ok(result);
        }

        [HttpGet]
        [Permission(AllPermissions.RouteView)]
        public async Task<ActionResult<PagedResult<InventoryRouteDto>>> GetAllRoutes(
            [FromQuery] int? pageNumber = 1,
            [FromQuery] int? pageSize = 20,
            [FromQuery] bool? isCompleted = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var result = await _mediator.Send(new GetAllRoutesQuery(pageNumber, pageSize, isCompleted, startDate, endDate));
            return Ok(result);
        }

        [HttpGet("{id}")]
        [Permission(AllPermissions.RouteView)]
        public async Task<ActionResult<InventoryRouteDto>> GetById(int id)
        {
            var result = await _mediator.Send(new GetRouteByIdQuery(id));
            if (result == null)
                return NotFound();
            return Ok(result);
        }

        [HttpGet("department/{departmentId}")]
        [Permission(AllPermissions.RouteView)]
        public async Task<ActionResult<IEnumerable<InventoryRouteDto>>> GetByDepartment(int departmentId)
        {
            var result = await _mediator.Send(new GetRoutesByDepartmentQuery(departmentId));
            return Ok(result);
        }

        [HttpGet("incomplete")]
        [Permission(AllPermissions.RouteView)]
        public async Task<ActionResult<IEnumerable<InventoryRouteDto>>> GetIncompleteRoutes()
        {
            var result = await _mediator.Send(new GetIncompleteRoutesQuery());
            return Ok(result);
        }

        [HttpPut("{id}")]
        [Consumes("multipart/form-data")]
        [Permission(AllPermissions.RouteUpdate)]
        public async Task<IActionResult> UpdateRoute(int id, [FromForm] UpdateRouteDto dto)
        {
            await _mediator.Send(new UpdateRoute.Command(id, dto));
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Permission(AllPermissions.RouteDelete)]
        public async Task<IActionResult> DeleteRoute(int id)
        {
            await _mediator.Send(new DeleteRoute.Command(id));
            return NoContent();
        }

        [HttpPut("{id}/complete")]
        [Permission(AllPermissions.RouteComplete)]
        public async Task<IActionResult> CompleteRoute(int id)
        {
            await _mediator.Send(new CompleteRoute.Command(id));
            return NoContent();
        }


        [HttpPost("batch-delete")]
        [Permission(AllPermissions.RouteBatchDelete)]
        public async Task<ActionResult<BatchDeleteResultDto>> BatchDelete(BatchDeleteDto dto)
        {
            var result = await _mediator.Send(new BatchDeleteRoutes.Command(dto));
            return Ok(result);
        }
    }
}