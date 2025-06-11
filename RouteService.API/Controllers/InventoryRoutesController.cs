using MediatR;
using Microsoft.AspNetCore.Mvc;
using RouteService.Application.DTOs;
using RouteService.Application.DTOs.Commands;
using RouteService.Application.Features.Routes.Commands;
using RouteService.Application.Features.Routes.Queries;

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


        [HttpPost("remove")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<InventoryRouteDto>> RemoveInventory([FromForm] RemoveInventoryDto dto)
        {
            var result = await _mediator.Send(new RemoveInventory.Command(dto));
            return Ok(result);
        }


        [HttpGet("product/{productId}")]
        public async Task<ActionResult<IEnumerable<InventoryRouteDto>>> GetInventoryByProductId(int productId)
        {
            var result = await _mediator.Send(new GetRoutesByProductQuery(productId));
            return Ok(result);
        }
    }
}