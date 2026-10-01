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
    /// <remarks>
    /// Errors (not found, duplicate → 409, approval required → 202, insufficient permissions → 403)
    /// are mapped to JSON by the host's API exception middleware.
    /// </remarks>
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


        /// <summary>
        /// The (department, category) pairs present in the inventory, used to cascade the list
        /// filters. The state/quick filters narrow the result so the options react to them.
        /// </summary>
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


        // Also for the Transfer page: whoever may transfer must be able to find the product.
        [HttpGet("search/inventory-code/{inventoryCode}")]
        [Permission(AllPermissions.ProductView, AllPermissions.RouteCreate, AllPermissions.RouteCreateDirect)]
        public async Task<ActionResult<ProductDto>> GetByInventoryCode(int inventoryCode)
        {
            var product = await _mediator.Send(new GetProductByInventoryCodeQuery(inventoryCode));
            return product == null ? NotFound() : Ok(product);
        }


        /// <summary>Direct with product.create.direct, otherwise queued for approval (202).</summary>
        [HttpPost]
        [Consumes("multipart/form-data")]
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
