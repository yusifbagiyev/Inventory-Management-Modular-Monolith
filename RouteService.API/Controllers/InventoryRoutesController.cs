using System.Security.Claims;
using System.Text.Json;
using IdentityService.Domain.Constants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RouteService.Application.DTOs;
using RouteService.Application.Features.Routes.Commands;
using RouteService.Application.Features.Routes.Queries;
using RouteService.Application.Interfaces;
using RouteService.Domain.Common;
using SharedServices.Authorization;
using SharedServices.DTOs;
using SharedServices.Enum;

namespace RouteService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class InventoryRoutesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IApprovalService _approvalService;
        private readonly ILogger<InventoryRoutesController> _logger;
        public InventoryRoutesController(
            IMediator mediator,
            IApprovalService approvalService,
            ILogger<InventoryRoutesController> logger)
        {
            _mediator = mediator;
            _approvalService = approvalService;
            _logger = logger;
        }


        [HttpPost("transfer")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<InventoryRouteDto>> TransferInventory([FromForm] TransferInventoryDto dto)
        {
            var userId=int.Parse(User.FindFirst("UserId")?.Value ?? "0");
            var userName = User.Identity?.Name ?? "Unknown";

            if(User.HasClaim("permission", AllPermissions.RouteCreateDirect))
            {
                var result=await _mediator.Send(new TransferInventory.Command(dto));
                return Ok(result);
            }
            else if(User.HasClaim("permission", AllPermissions.RouteCreate))
            {
                var actionData=new Dictionary<string, object>
                {
                    ["productId"] = dto.ProductId,
                    ["toDepartmentId"] = dto.ToDepartmentId,
                    ["toWorker"] = dto.ToWorker ?? "",
                    ["notes"] = dto.Notes ?? ""
                };

                //Add image data if available
                if(dto.ImageFile != null && dto.ImageFile.Length > 0)
                {
                    using var ms=new MemoryStream();
                    await dto.ImageFile.CopyToAsync(ms);
                    actionData["image"] = Convert.ToBase64String(ms.ToArray());
                    actionData["imageFileName"] = dto.ImageFile.FileName;
                }

                var approvalRequest = new CreateApprovalRequestDto
                {
                    RequestType = RequestType.TransferProduct,
                    EntityType = "Route",
                    EntityId = null,
                    ActionData=actionData
                };

                var result=await _approvalService.CreateApprovalRequestAsync(approvalRequest, userId, userName);

                return Accepted(new
                {
                    ApprovalRequestId = result.Id,
                    Message = "Transfer request has been submitted for approval",
                    Status = "PendingApproval"
                });
            }
            return Forbid();
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
        public async Task<IActionResult> UpdateRoute(int id, [FromForm] UpdateRouteDto dto)
        {
            var userId = int.Parse(User.FindFirst("UserId")?.Value ?? "0");
            var userName = User.Identity?.Name ?? "Unknown";

            if(User.HasClaim("permission", AllPermissions.RouteUpdateDirect))
            {
                await _mediator.Send(new UpdateRoute.Command(id, dto));
                return NoContent();
            }
            else if(User.HasClaim("permission", AllPermissions.RouteUpdate))
            {
                var existingRoute=await _mediator.Send(new GetRouteByIdQuery(id));
                if (existingRoute == null)
                    return NotFound();

                var updateData = new Dictionary<string, object>
                {
                    ["notes"] = dto.Notes ?? ""
                };

                //Add image data if available
                if(dto.ImageFile != null && dto.ImageFile.Length > 0)
                {
                    using var ms = new MemoryStream();
                    await dto.ImageFile.CopyToAsync(ms);
                    updateData["image"] = Convert.ToBase64String(ms.ToArray());
                    updateData["imageFileName"] = dto.ImageFile.FileName;
                }

                var approvalRequest = new CreateApprovalRequestDto
                {
                    RequestType = RequestType.UpdateRoute,
                    EntityType = "Route",
                    EntityId = id,
                    ActionData = new
                    {
                        RouteId = id,
                        UpdateData = updateData,
                        existingRoute.InventoryCode,
                        existingRoute.Model,
                        existingRoute.FromDepartmentName,
                        existingRoute.ToDepartmentName
                    }
                };

                var result = await _approvalService.CreateApprovalRequestAsync(approvalRequest, userId, userName);
                return Accepted(
                    new 
                    { 
                        ApprovalRequestId = result.Id, 
                        Message = "Route update request submitted for approval" 
                    });

            }
            return Forbid();
        }



        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRoute(int id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var userName = User.Identity?.Name ?? "Unknown";

            if (User.HasClaim("permission", AllPermissions.RouteDeleteDirect))
            {
                await _mediator.Send(new DeleteRoute.Command(id));
                return NoContent();
            }
            else if (User.HasClaim("permission", AllPermissions.RouteDelete))
            {
                var route = await _mediator.Send(new GetRouteByIdQuery(id));
                if (route == null)
                    return NotFound();

                var approvalRequest = new CreateApprovalRequestDto
                {
                    RequestType = RequestType.DeleteRoute,
                    EntityType = "Route",
                    EntityId = id,
                    ActionData = new DeleteRouteActionData
                    {
                        RouteId = id
                    }
                };

                var result = await _approvalService.CreateApprovalRequestAsync(approvalRequest, userId, userName);
                return Accepted(new { ApprovalRequestId = result.Id, Message = "Route deletion request submitted for approval" });
            }
            return Forbid();
        }



        [HttpPost("transfer/approved")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<InventoryRouteDto>> TransferApprovedMultipart([FromForm] TransferInventoryDto dto)
        {
            try
            {
                _logger.LogInformation($"Executing approved transfer for product {dto.ProductId} to department {dto.ToDepartmentId}");
                if (dto.ProductId <= 0)
                {
                    _logger.LogError("Invalid ProductId: {ProductId}", dto.ProductId);
                    return BadRequest(new { error = "Invalid ProductId" });
                }

                if (dto.ToDepartmentId <= 0)
                {
                    _logger.LogError("Invalid ToDepartmentId: {ToDepartmentId}", dto.ToDepartmentId);
                    return BadRequest(new { error = "Invalid ToDepartmentId" });
                }

                var result = await _mediator.Send(new TransferInventory.Command(dto));
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing approved transfer");
                return BadRequest(new { error = ex.Message, details = ex.InnerException?.Message });
            }
        }



        [HttpPut("{id}/approved")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateApproved(int id, [FromBody] object updateData)
        {
            var json = updateData.ToString();
            var data = JsonSerializer.Deserialize<JsonElement>(json!);

            var dto = new UpdateRouteDto
            {
                Notes = data.TryGetProperty("notes", out var notes) ? notes.GetString() : null
            };

            await _mediator.Send(new UpdateRoute.Command(id, dto));
            return NoContent();
        }



        [HttpDelete("{id}/approved")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteApproved(int id)
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