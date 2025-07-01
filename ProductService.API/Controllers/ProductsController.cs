using IdentityService.Domain.Constants;
using IdentityService.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductService.Application.DTOs;
using ProductService.Application.Features.Products.Commands;
using ProductService.Application.Features.Products.Queries;

namespace ProductService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Require authentication for all endpoints
    public class ProductsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ProductsController(IMediator mediator)
        {
            _mediator = mediator;
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
        [Permission(AllPermissions.ProductCreate)]
        public async Task<ActionResult<ProductDto>> Create([FromForm] CreateProductDto dto)
        {
            var product = await _mediator.Send(new CreateProduct.Command(dto));
            return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
        }


        [HttpPut("{id}")]
        [Consumes("multipart/form-data")]
        [Permission(AllPermissions.ProductUpdate)]
        public async Task<IActionResult> Update(int id, UpdateProductDto dto)
        {
            await _mediator.Send(new UpdateProduct.Command(id, dto));
            return NoContent();
        }


        [HttpDelete("{id}")]
        [Permission(AllPermissions.ProductDelete)]
        public async Task<IActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteProduct.Command(id));
            return NoContent();
        }
    }
}
