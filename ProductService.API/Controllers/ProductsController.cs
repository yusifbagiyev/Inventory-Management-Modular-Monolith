using System.Security.Claims;
using ApprovalService.Domain.Enums;
using IdentityService.Domain.Constants;
using IdentityService.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductService.Application.DTOs;
using ProductService.Application.Features.Products.Commands;
using ProductService.Application.Features.Products.Queries;
using ProductService.Application.Interfaces;

namespace ProductService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Require authentication for all endpoints
    public class ProductsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IApprovalService _approvalService;

        public ProductsController(IMediator mediator,IApprovalService approvalService)
        {
            _mediator = mediator;
            _approvalService = approvalService;
        }


        [HttpGet]
        [Permission(AllPermissions.ProductView)]
        public async Task<ActionResult<IEnumerable<ProductDto>>> GetAll()
        {
            var products = await _mediator.Send(new GetAllProductsQuery());
            return Ok(products);
        }


        [HttpGet("{id}")]
        [Permission(AllPermissions.ProductView)]
        public async Task<ActionResult<ProductDto>> GetById(int id)
        {
            var product = await _mediator.Send(new GetProductByIdQuery(id));
            if (product == null)
                return NotFound();
            return Ok(product);
        }


        [HttpGet("search/inventory-code/{inventoryCode}")]
        [Permission(AllPermissions.ProductView)]
        public async Task<ActionResult<ProductDto>> GetByInventoryCode(int inventoryCode)
        {
            var product = await _mediator.Send(new GetProductByInventoryCodeQuery(inventoryCode));
            if (product == null)
                return NotFound();
            return Ok(product);
        }


        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ProductDto>> Create([FromForm] CreateProductDto dto)
        {
            var userId=int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value??"0");
            var userName = User.Identity?.Name ?? "Unknown";

            //Check if user has direct permission
            if (User.HasClaim("permission", AllPermissions.ProductCreateDirect))
            {
                var product = await _mediator.Send(new CreateProduct.Command(dto));
                return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
            }

            //Check if user has request permission
            else if (User.HasClaim("permission", AllPermissions.ProductCreate))
            {
                //Create approval request instead
                var approvalRequest = new CreateApprovalRequestDto
                {
                    RequestType = RequestType.CreateProduct,
                    EntityType = "Product",
                    EntityId = null,
                    ActionData = dto
                };

                var result = await _approvalService.CreateApprovalRequestAsync(approvalRequest, userId, userName);
                return Accepted(new { ApprovalRequestId = result.Id, Message = "Product creation request submitted for approval" });
            }
            return Forbid();
        }


        [HttpPut("{id}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Update(int id,[FromForm] UpdateProductDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var userName = User.Identity?.Name ?? "Unknown";

            if (User.HasClaim("permission", AllPermissions.ProductUpdateDirect))
            {
                await _mediator.Send(new UpdateProduct.Command(id, dto));
                return NoContent();
            }
            else if (User.HasClaim("permission", AllPermissions.ProductUpdate))
            {
                var approvalRequest = new CreateApprovalRequestDto
                {
                    RequestType = RequestType.UpdateProduct,
                    EntityType = "Product",
                    EntityId = id,
                    ActionData = new { ProductId = id, UpdateData = dto }
                };

                var result = await _approvalService.CreateApprovalRequestAsync(approvalRequest, userId, userName);
                return Accepted(new { ApprovalRequestId = result.Id, Message = "Product update request submitted for approval" });
            }
            return Forbid();
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var userName = User.Identity?.Name ?? "Unknown";

            if (User.HasClaim("permission", AllPermissions.ProductDeleteDirect))
            {
                await _mediator.Send(new DeleteProduct.Command(id));
                return NoContent();
            }
            else if (User.HasClaim("permission", AllPermissions.ProductDelete))
            {
                var approvalRequest = new CreateApprovalRequestDto
                {
                    RequestType = RequestType.DeleteProduct,
                    EntityType = "Product",
                    EntityId = id,
                    ActionData = new { ProductId = id }
                };

                var result = await _approvalService.CreateApprovalRequestAsync(approvalRequest, userId, userName);
                return Accepted(new { ApprovalRequestId = result.Id, Message = "Product deletion request submitted for approval" });
            }
            return Forbid();
        }


        [HttpPost("approved")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [Authorize(Policy = "SystemOnly")]
        public async Task<ActionResult<ProductDto>> CreateApproved([FromBody] CreateProductDto dto)
        {
            var product = await _mediator.Send(new CreateProduct.Command(dto));
            return Ok(product);
        }
    }
}