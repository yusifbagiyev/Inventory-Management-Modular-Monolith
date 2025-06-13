using MediatR;
using Microsoft.AspNetCore.Mvc;
using RouteService.Application.DTOs;
using RouteService.Application.Features.Routes.Commands;
using RouteService.Application.Features.Routes.Queries;
using RouteService.Domain.Common;

namespace RouteService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryRoutesController : ControllerBase
    {
        private readonly IMediator _mediator;
        public InventoryRoutesController(IMediator mediator)
        {
            _mediator = mediator;
        }
        [HttpPost("transfer")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<InventoryRouteDto>> TransferInventory([FromForm] TransferInventoryDto dto)
        {
            var result = await _mediator.Send(new TransferInventory.Command(dto));
            return Ok(result);
        }


        [HttpGet("product/{productId}")]
        public async Task<ActionResult<IEnumerable<InventoryRouteDto>>> GetInventoryByProductId(int productId)
        {
            var result = await _mediator.Send(new GetRoutesByProductQuery(productId));
            return Ok(result);
        }

        [HttpGet]
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
        public async Task<ActionResult<InventoryRouteDto>> GetById(int id)
        {
            var result = await _mediator.Send(new GetRouteByIdQuery(id));
            if (result == null)
                return NotFound();
            return Ok(result);
        }

        [HttpGet("department/{departmentId}")]
        public async Task<ActionResult<IEnumerable<InventoryRouteDto>>> GetByDepartment(int departmentId)
        {
            var result = await _mediator.Send(new GetRoutesByDepartmentQuery(departmentId));
            return Ok(result);
        }

        [HttpGet("incomplete")]
        public async Task<ActionResult<IEnumerable<InventoryRouteDto>>> GetIncompleteRoutes()
        {
            var result = await _mediator.Send(new GetIncompleteRoutesQuery());
            return Ok(result);
        }
    }
}