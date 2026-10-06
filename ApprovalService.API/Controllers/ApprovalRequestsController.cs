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
    /// <summary>Approval requests API without a create endpoint, since only the owning modules submit requests.</summary>
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

        // API-key clients have no numeric user id and get 0 instead of a 500
        private int CurrentUserId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        private string CurrentUserName => User.Identity?.Name ?? "Unknown";

        // The paged lists stay plain arrays for their callers, so the total travels in a header
        private const string TotalCountHeader = "X-Total-Count";


        [HttpGet]
        [Permission(AllPermissions.ApprovalView, AllPermissions.ApprovalDecide)]
        public async Task<ActionResult<PagedResultDto<ApprovalRequestDto>>> GetPending(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _mediator.Send(new GetPendingRequests.Query(pageNumber, pageSize));
            return Ok(result);
        }


        /// <summary>Count only, so the menu badge never reads a request.</summary>
        [HttpGet("pending-count")]
        [Permission(AllPermissions.ApprovalView, AllPermissions.ApprovalDecide)]
        public async Task<IActionResult> GetPendingCount()
        {
            return Ok(new { count = await _mediator.Send(new GetPendingCount.Query()) });
        }


        [HttpGet("my-requests")]
        public async Task<ActionResult<IEnumerable<ApprovalRequestDto>>> GetMyRequests(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = GetUserRequests.DefaultPageSize)
        {
            var result = await _mediator.Send(new GetUserRequests.Query(CurrentUserId, pageNumber, pageSize));
            Response.Headers[TotalCountHeader] = result.TotalCount.ToString();
            return Ok(result.Items);
        }


        [HttpGet("{id}")]
        public async Task<ActionResult<ApprovalRequestDto>> GetRequestById(int id)
        {
            var approvalRequest = await _mediator.Send(new GetRequestById.Query(id));

            // Only the requester or an approver may read a request, and NotFound hides whether it exists
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
            var executed = await _mediator.Send(new ApproveRequest.Command(id, CurrentUserId, CurrentUserName, User.IsInRole(AllRoles.Admin)));
            if (executed)
                return NoContent();

            // The approval is recorded, but its action was rolled back and the request is now Failed
            var failed = await _mediator.Send(new GetRequestById.Query(id, WithImageData: false));
            return Conflict(new
            {
                error = failed?.RejectionReason ?? "The request was approved but its action failed to execute.",
                status = failed?.Status ?? "Failed",
                approvalRequestId = id
            });
        }


        [HttpPost("{id}/reject")]
        [Permission(AllPermissions.ApprovalDecide)]
        public async Task<IActionResult> Reject(int id, RejectRequestDto dto)
        {
            await _mediator.Send(new RejectRequest.Command(id, CurrentUserId, CurrentUserName, dto.Reason?.Trim() ?? ""));
            return NoContent();
        }


        [HttpGet("all")]
        [Permission(AllPermissions.ApprovalView, AllPermissions.ApprovalDecide)]
        public async Task<ActionResult<IEnumerable<ApprovalRequestDto>>> GetAllRequests(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = Application.Features.Queries.GetAllRequests.DefaultPageSize)
        {
            var result = await _mediator.Send(new GetAllRequests.Query(pageNumber, pageSize));
            Response.Headers[TotalCountHeader] = result.TotalCount.ToString();
            return Ok(result.Items);
        }


        [HttpDelete("{id}/cancel")]
        public async Task<IActionResult> Cancel(int id)
        {
            await _mediator.Send(new CancelRequest.Command(id, CurrentUserId));
            return NoContent();
        }
    }
}
