using InventoryManagement.Web.Extensions;
using System.Security.Claims;
using ApprovalService.Application.Features.Commands;
using ApprovalService.Application.Features.Queries;
using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Services.Interfaces;
using MediatR;
using SharedServices.Identity;

namespace InventoryManagement.Web.Services
{
    /// <summary>The approval inbox for the UI, backed in-process by the approval module.</summary>
    public class ApprovalService : IApprovalService
    {
        private const int PendingPageSize = 100;

        private readonly IMediator _mediator;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ApprovalService(IMediator mediator, IHttpContextAccessor httpContextAccessor)
        {
            _mediator = mediator;
            _httpContextAccessor = httpContextAccessor;
        }

        private ClaimsPrincipal User => _httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();

        private int UserId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        private string UserName => User.Identity?.Name ?? "Unknown";

        public async Task<List<ApprovalRequestDto>> GetPendingRequestsAsync()
        {
            var page = await _mediator.Send(new GetPendingRequests.Query(1, PendingPageSize));
            return ModelMapper.MapList<ApprovalRequestDto>(page.Items);
        }

        /// <summary>Null when the request is missing or belongs to someone else and the user cannot see all requests.</summary>
        public async Task<ApprovalRequestDto?> GetRequestDetailsAsync(int id)
        {
            var request = await _mediator.Send(new GetRequestById.Query(id));
            var canSeeAll = User.HasPermission(AllPermissions.ApprovalView) || User.HasPermission(AllPermissions.ApprovalDecide);
            if (request == null || (!canSeeAll && request.RequestedById != UserId))
                return null;
            return ModelMapper.Map<ApprovalRequestDto>(request);
        }

        public Task<bool> ApproveRequestAsync(int id)
            => _mediator.Send(new ApproveRequest.Command(id, UserId, UserName, User.IsInRole(AllRoles.Admin)));

        public Task RejectRequestAsync(int id, string reason)
            => _mediator.Send(new RejectRequest.Command(id, UserId, UserName, reason));

        public async Task<ApprovalStatisticsDto> GetStatisticsAsync()
        {
            var stats = await _mediator.Send(new GetApprovalStatistics.Query());
            return new ApprovalStatisticsDto
            {
                TotalPending = stats.Pending,
                TotalApprovedToday = stats.ExecutedToday,
                TotalRejectedToday = stats.RejectedToday
            };
        }

        public async Task<(List<ApprovalRequestDto> Items, int ApprovedCount, int RejectedCount)> GetDecidedRequestsAsync(bool? approved, int take)
        {
            var result = await _mediator.Send(new GetDecidedRequests.Query(approved, take));
            return (ModelMapper.MapList<ApprovalRequestDto>(result.Items), result.ApprovedCount, result.RejectedCount);
        }

        public async Task<List<ApprovalRequestDto>> GetMyRequestsAsync()
            => ModelMapper.MapList<ApprovalRequestDto>(await _mediator.Send(new GetUserRequests.Query(UserId)));

        public Task CancelRequestAsync(int id)
            => _mediator.Send(new CancelRequest.Command(id, UserId));
    }
}
