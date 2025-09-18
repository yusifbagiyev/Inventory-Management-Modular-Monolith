using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductService.Application.DTOs;
using ProductService.Application.Features.Products.Commands;
using ProductService.Application.Features.Products.Queries;
using ProductService.Application.Interfaces;
using ProductService.Domain.Common;
using SharedServices.Authorization;
using SharedServices.Exceptions;
using SharedServices.Identity;
using System.Security.Claims;
using System.Text.Json;

namespace ProductService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProductsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IProductManagementService _productManagementService;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(IMediator mediator, IProductManagementService productManagementService, ILogger<ProductsController> logger)
        {
            _mediator = mediator; 
            _productManagementService = productManagementService;
            _logger = logger;
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
            [FromQuery] int? departmentId = null)
        {
            var products = await _mediator.Send(new GetAllProductsQuery(
                pageNumber, pageSize, search, startDate, endDate,
                status, availability, categoryId, departmentId));
            return Ok(products);
        }



        [HttpGet("{id}")]
        [Permission(AllPermissions.ProductView)]
        public async Task<ActionResult<ProductDto>> GetById(int id)
        {
            var product = await _mediator.Send(new GetProductByIdQuery(id));
            return product == null ? NotFound() : Ok(product);
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
        public async Task<IActionResult> Create([FromForm] CreateProductDto dto)
        {
            try
            {
                var userId = GetUserId();
                var userName = GetUserName();
                var userPermissions = GetUserPermissions();

                var product = await _productManagementService.CreateProductWithApprovalAsync(
                    dto, userId, userName, userPermissions);

                return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
            }
            catch (ApprovalRequiredException ex)
            {
                return Accepted(new
                {
                    ApprovalRequestId = ex.ApprovalRequestId,
                    Message = ex.Message,
                    Status = ex.Status
                });
            }
            catch (DuplicateEntityException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InsufficientPermissionsException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating product");
                return StatusCode(500, new { error = "An unexpected error occurred" });
            }
        }



        [HttpPut("{id}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Update(int id, [FromForm] UpdateProductDto dto)
        {
            try
            {
                var userId = GetUserId();
                var userName = GetUserName();
                var userPermissions = GetUserPermissions();

                var product = await _productManagementService.UpdateProductWithApprovalAsync(
                    id, dto, userId, userName, userPermissions);

                return NoContent();
            }
            catch (ApprovalRequiredException ex)
            {
                return Accepted(new
                {
                    ApprovalRequestId = ex.ApprovalRequestId,
                    Message = ex.Message,
                    Status = ex.Status
                });
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InsufficientPermissionsException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error updating product {ProductId}", id);
                return StatusCode(500, new { error = "An unexpected error occurred" });
            }
        }



        [HttpPost("approved/multipart")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ProductDto>> CreateApprovedMultipart([FromForm] CreateProductDto dto)
        {
            try
            {
                var product = await _mediator.Send(new CreateProduct.Command(dto));
                return Ok(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating approved product");
                return BadRequest(new { error = ex.Message });
            }
        }



        [HttpPut("{id}/approved")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateApprovedMultipart(int id, [FromForm] UpdateProductDto dto)
        {
            try
            {
                await _mediator.Send(new UpdateProduct.Command(id, dto));
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating approved product with multipart data");
                return BadRequest(new { error = ex.Message, details = ex.InnerException?.Message });
            }
        }



        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var userId = GetUserId();
                var userName = GetUserName();
                var userPermissions = GetUserPermissions();

                await _productManagementService.DeleteProductWithApprovalAsync(
                    id, userId, userName, userPermissions);

                return NoContent();
            }
            catch (ApprovalRequiredException ex)
            {
                return Accepted(new
                {
                    ApprovalRequestId = ex.ApprovalRequestId,
                    Message = ex.Message,
                    Status = ex.Status
                });
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InsufficientPermissionsException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error deleting product {ProductId}", id);
                return StatusCode(500, new { error = "An unexpected error occurred" });
            }
        }



        [HttpPost("approved")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ProductDto>> CreateApproved([FromBody] JsonElement productData)
        {
            try
            {
                // Create a DTO without the file
                var dto = new CreateProductDto
                {
                    InventoryCode = productData.GetProperty("inventoryCode").GetInt32(),
                    Model = productData.TryGetProperty("model", out var model) ? model.GetString() : "",
                    Vendor = productData.TryGetProperty("vendor", out var vendor) ? vendor.GetString() : "",
                    Worker = productData.TryGetProperty("worker", out var worker) ? worker.GetString() : "",
                    Description = productData.TryGetProperty("description", out var desc) ? desc.GetString() : "",
                    IsWorking = productData.TryGetProperty("isWorking", out var working) ? working.GetBoolean() : true,
                    IsActive = productData.TryGetProperty("isActive", out var active) ? active.GetBoolean() : true,
                    IsNewItem = productData.TryGetProperty("isNewItem", out var newItem) ? newItem.GetBoolean() : true,
                    CategoryId = productData.GetProperty("categoryId").GetInt32(),
                    DepartmentId = productData.GetProperty("departmentId").GetInt32()
                };

                var product = await _mediator.Send(new CreateProduct.Command(dto));
                return Ok(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating approved product");
                return BadRequest($"Error: {ex.Message}");
            }
        }




        [HttpPut("{id}/approved")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateApproved(int id, [FromBody] object updateData)
        {
            var json = updateData.ToString();
            var dto = JsonSerializer.Deserialize<UpdateProductDto>(json!);

            if (dto == null)
                return BadRequest("Invalid update data");

            await _mediator.Send(new UpdateProduct.Command(id, dto));
            return NoContent();
        }



        [HttpDelete("{id}/approved")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteApproved(int id)
        {
            var userName = User.Identity?.Name ?? "Unknown";
            await _mediator.Send(new DeleteProduct.Command(id,userName));
            return NoContent();
        }


        [HttpPut("{id}/inventory-code")]
        [Permission(AllPermissions.ProductUpdate)]
        public async Task<IActionResult> UpdateInventoryCode(int id, [FromBody] UpdateInventoryCodeDto dto)
        {
            // Add logic to update only inventory code
            var product = await _mediator.Send(new GetProductByIdQuery(id));
            if (product == null)
                return NotFound();

            // Check if new code already exists
            var existing = await _mediator.Send(new GetProductByInventoryCodeQuery(dto.InventoryCode));
            if (existing != null && existing.Id != id)
                return BadRequest(new { error = "Inventory code already exists" });

            // Update only the inventory code
            await _mediator.Send(new UpdateProductInventoryCode.Command(id, dto.InventoryCode));
            return NoContent();
        }



        // Helper methods to extract user information
        private int GetUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }

        private string GetUserName()
        {
            return User.Identity?.Name ?? "Unknown";
        }

        private List<string> GetUserPermissions()
        {
            return User.Claims
                .Where(c => c.Type == "permission")
                .Select(c => c.Value)
                .ToList();
        }
    }
}