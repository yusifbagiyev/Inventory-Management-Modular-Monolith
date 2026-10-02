using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductService.Application.DTOs;
using ProductService.Application.Features.Products.Commands;
using ProductService.Application.Features.Products.Queries;
using ProductService.Application.Interfaces;
using ProductService.Domain.Common;
using SharedServices.Authorization;
using SharedServices.Identity;

namespace ProductService.API.Controllers
{
    /// <remarks>Module exceptions are turned into status codes by the host's API exception middleware.</remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProductsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IProductManagementService _productManagementService;

        public ProductsController(IMediator mediator, IProductManagementService productManagementService)
        {
            _mediator = mediator;
            _productManagementService = productManagementService;
        }


        [HttpGet]
        [Permission(AllPermissions.ProductView)]
        public async Task<ActionResult<PagedResult<ProductDto>>> GetAll(
            [FromQuery] int? pageNumber = 1,
            [FromQuery] int? pageSize = 30,
            [FromQuery] string? search = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] bool? status = null,
            [FromQuery] bool? availability = null,
            [FromQuery] int? categoryId = null,
            [FromQuery] int? departmentId = null,
            [FromQuery] bool? hasImage = null,
            [FromQuery] bool? assigned = null)
        {
            var products = await _mediator.Send(new GetAllProductsQuery(
                pageNumber, pageSize, search, startDate, endDate,
                status, availability, categoryId, departmentId, hasImage, assigned));
            return Ok(products);
        }


        /// <summary>Department and category pairs in use, so the list filters can cascade.</summary>
        [HttpGet("filter-facets")]
        [Permission(AllPermissions.ProductView)]
        public async Task<ActionResult<ProductFilterFacetsDto>> GetFilterFacets(
            [FromQuery] bool? status = null,
            [FromQuery] bool? availability = null,
            [FromQuery] bool? hasImage = null,
            [FromQuery] bool? assigned = null)
        {
            var facets = await _mediator.Send(new GetProductFilterFacetsQuery(status, availability, hasImage, assigned));
            return Ok(facets);
        }


        [HttpGet("{id}")]
        [Permission(AllPermissions.ProductView)]
        public async Task<ActionResult<ProductDto>> GetById(int id)
        {
            var product = await _mediator.Send(new GetProductByIdQuery(id));
            return product == null ? NotFound() : Ok(product);
        }


        // The Transfer page uses this too, so anyone who may transfer must be able to find the product.
        [HttpGet("search/inventory-code/{inventoryCode}")]
        [Permission(AllPermissions.ProductView, AllPermissions.RouteCreate, AllPermissions.RouteCreateDirect)]
        public async Task<ActionResult<ProductDto>> GetByInventoryCode(int inventoryCode)
        {
            var product = await _mediator.Send(new GetProductByInventoryCodeQuery(inventoryCode));
            return product == null ? NotFound() : Ok(product);
        }


        /// <summary>Creates directly with product.create.direct, otherwise queues it for approval and answers 202.</summary>
        [HttpPost]
        [Consumes("multipart/form-data")]
        [Permission(AllPermissions.ProductCreate, AllPermissions.ProductCreateDirect)]
        public async Task<IActionResult> Create([FromForm] CreateProductDto dto)
        {
            var product = await _productManagementService.CreateProductWithApprovalAsync(
                dto,
                _productManagementService.GetUserId(User),
                _productManagementService.GetUserName(User),
                _productManagementService.GetUserPermissions(User));
            return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
        }


        [HttpPut("{id}")]
        [Consumes("multipart/form-data")]
        [Permission(AllPermissions.ProductUpdate, AllPermissions.ProductUpdateDirect)]
        public async Task<IActionResult> Update(int id, [FromForm] UpdateProductDto dto)
        {
            await _productManagementService.UpdateProductWithApprovalAsync(
                id,
                dto,
                _productManagementService.GetUserId(User),
                _productManagementService.GetUserName(User),
                _productManagementService.GetUserPermissions(User));
            return NoContent();
        }


        [HttpDelete("{id}")]
        [Permission(AllPermissions.ProductDelete, AllPermissions.ProductDeleteDirect)]
        public async Task<IActionResult> Delete(int id)
        {
            await _productManagementService.DeleteProductWithApprovalAsync(
                id,
                _productManagementService.GetUserId(User),
                _productManagementService.GetUserName(User),
                _productManagementService.GetUserPermissions(User));
            return NoContent();
        }


        [HttpPut("{id}/inventory-code")]
        [Permission(AllPermissions.ProductCodeUpdate)]
        public async Task<IActionResult> UpdateInventoryCode(int id, [FromBody] UpdateInventoryCodeDto dto)
        {
            await _mediator.Send(new UpdateProductInventoryCode.Command(id, dto.InventoryCode));
            return NoContent();
        }
    }
}
