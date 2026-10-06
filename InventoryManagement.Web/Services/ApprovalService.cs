using InventoryManagement.Web.Extensions;
using System.Security.Claims;
using ApprovalService.Application.Features.Commands;
using ApprovalService.Application.Features.Queries;
using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using MediatR;
using SharedServices.Identity;

namespace InventoryManagement.Web.Services
{
    /// <summary>The approval inbox for the UI, backed in-process by the approval module.</summary>
    public class ApprovalService : IApprovalService
    {
        private const int PendingPageSize = 100;
        private const int MyRequestsPageSize = 200;

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
            // The pages show the uploads' names only, so the image bytes stay in the database
            var request = await _mediator.Send(new GetRequestById.Query(id, WithImageData: false));
            var canSeeAll = User.HasPermission(AllPermissions.ApprovalView) || User.HasPermission(AllPermissions.ApprovalDecide);
            if (request == null || (!canSeeAll && request.RequestedById != UserId))
                return null;
            return ModelMapper.Map<ApprovalRequestDto>(request);
        }

        public async Task<(byte[] Data, string ContentType)?> GetRequestImageAsync(int id, int index)
        {
            var request = await _mediator.Send(new GetRequestById.Query(id, WithImageData: true));
            var canSeeAll = User.HasPermission(AllPermissions.ApprovalView) || User.HasPermission(AllPermissions.ApprovalDecide);
            if (request == null || (!canSeeAll && request.RequestedById != UserId))
                return null;

            var images = ApprovalView.From(request.RequestType, request.ActionData).NewImages();
            if (index < 0 || index >= images.Count) return null;
            var image = images[index];
            var base64 = (image.GetValue("imageData", StringComparison.OrdinalIgnoreCase) ?? image.GetValue("image", StringComparison.OrdinalIgnoreCase))?.ToString();
            // Decided requests keep the names only, their bytes are removed
            if (string.IsNullOrEmpty(base64)) return null;
            var comma = base64.IndexOf(',');
            if (comma >= 0) base64 = base64[(comma + 1)..];

            byte[] data;
            try { data = Convert.FromBase64String(base64); }
            catch (FormatException) { return null; }
            var name = image.GetValue("imageFileName", StringComparison.OrdinalIgnoreCase)?.ToString() ?? "";
            return (data, Path.GetExtension(name).Equals(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg");
        }

        public Task<bool> ApproveRequestAsync(int id)
            => _mediator.Send(new ApproveRequest.Command(id, UserId, UserName, User.IsInRole(AllRoles.Admin)));

        public Task RejectRequestAsync(int id, string reason)
            => _mediator.Send(new RejectRequest.Command(id, UserId, UserName, reason.Trim()));

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

        public async Task<(List<ApprovalRequestDto> Items, IReadOnlyDictionary<string, int> StatusCounts)> GetMyRequestsAsync()
        {
            var result = await _mediator.Send(new GetUserRequests.Query(UserId, 1, MyRequestsPageSize));
            return (ModelMapper.MapList<ApprovalRequestDto>(result.Items), result.StatusCounts);
        }

        public Task CancelRequestAsync(int id)
            => _mediator.Send(new CancelRequest.Command(id, UserId));
    }
}
