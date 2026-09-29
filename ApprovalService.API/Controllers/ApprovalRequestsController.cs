using System.Security.Claims;
using ApprovalService.Application.DTOs;
using ApprovalService.Application.Features.Commands;
using ApprovalService.Application.Features.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedServices.Identity;

namespace ApprovalService.API.Controllers
{
    /// <remarks>
    /// Requests are created by the owning modules (products, routes) when a user lacks the direct
    /// permission; there is deliberately no public "create" endpoint, which used to let any user
    /// submit arbitrary action data that bypassed those modules' checks.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ApprovalRequestsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ApprovalRequestsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        private int CurrentUserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

        private string CurrentUserName => User.Identity?.Name ?? "Unknown";


        [HttpGet]
        [Authorize(Roles = AllRoles.Admin)]
        public async Task<ActionResult<PagedResultDto<ApprovalRequestDto>>> GetPending(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _mediator.Send(new GetPendingRequests.Query(pageNumber, pageSize));
            return Ok(result);
        }


        [HttpGet("my-requests")]
        public async Task<ActionResult<IEnumerable<ApprovalRequestDto>>> GetMyRequests()
        {
            var result = await _mediator.Send(new GetUserRequests.Query(CurrentUserId));
            return Ok(result);
        }


        [HttpGet("{id}")]
        public async Task<ActionResult<ApprovalRequestDto>> GetRequestById(int id)
        {
            var approvalRequest = await _mediator.Send(new GetRequestById.Query(id));

            // Only the requester or an Admin may read a request (NotFound avoids an existence oracle).
            if (approvalRequest == null || (!User.IsInRole(AllRoles.Admin) && approvalRequest.RequestedById != CurrentUserId))
                return NotFound();

            return Ok(approvalRequest);
        }


        [HttpPost("{id}/approve")]
        [Authorize(Roles = AllRoles.Admin)]
        public async Task<IActionResult> Approve(int id)
        {
            await _mediator.Send(new ApproveRequest.Command(id, CurrentUserId, CurrentUserName));
            return NoContent();
        }


        [HttpPost("{id}/reject")]
        [Authorize(Roles = AllRoles.Admin)]
        public async Task<IActionResult> Reject(int id, RejectRequestDto dto)
        {
            await _mediator.Send(new RejectRequest.Command(id, CurrentUserId, CurrentUserName, dto.Reason));
            return NoContent();
        }


        [HttpGet("all")]
        [Authorize(Roles = AllRoles.Admin)]
        public async Task<ActionResult<IEnumerable<ApprovalRequestDto>>> GetAllRequests()
        {
            var result = await _mediator.Send(new GetAllRequests.Query());
            return Ok(result);
        }


        [HttpDelete("{id}/cancel")]
        public async Task<IActionResult> Cancel(int id)
        {
            await _mediator.Send(new CancelRequest.Command(id, CurrentUserId));
            return NoContent();
        }
    }
}
