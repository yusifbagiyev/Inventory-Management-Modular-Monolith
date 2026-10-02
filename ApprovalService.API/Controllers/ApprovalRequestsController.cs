using SharedServices.Authorization;
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
    /// <remarks>There is no create endpoint on purpose. Only the owning modules submit requests, after their own checks.</remarks>
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

        // API-key clients have no numeric user id and get 0 instead of a 500.
        private int CurrentUserId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        private string CurrentUserName => User.Identity?.Name ?? "Unknown";


        [HttpGet]
        [Permission(AllPermissions.ApprovalView, AllPermissions.ApprovalDecide)]
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

            // Only the requester or an approver may read a request. NotFound hides whether it exists.
            var canViewAll = User.IsInRole(AllRoles.Admin) || User.HasClaim("permission", AllPermissions.ApprovalView)
                || User.HasClaim("permission", AllPermissions.ApprovalDecide);
            if (approvalRequest == null || (!canViewAll && approvalRequest.RequestedById != CurrentUserId))
                return NotFound();

            return Ok(approvalRequest);
        }


        [HttpPost("{id}/approve")]
        [Permission(AllPermissions.ApprovalDecide)]
        public async Task<IActionResult> Approve(int id)
        {
            await _mediator.Send(new ApproveRequest.Command(id, CurrentUserId, CurrentUserName, User.IsInRole(AllRoles.Admin)));
            return NoContent();
        }


        [HttpPost("{id}/reject")]
        [Permission(AllPermissions.ApprovalDecide)]
        public async Task<IActionResult> Reject(int id, RejectRequestDto dto)
        {
            await _mediator.Send(new RejectRequest.Command(id, CurrentUserId, CurrentUserName, dto.Reason));
            return NoContent();
        }


        [HttpGet("all")]
        [Permission(AllPermissions.ApprovalView, AllPermissions.ApprovalDecide)]
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
