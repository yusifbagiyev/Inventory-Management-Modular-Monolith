using ApprovalService.Application.DTOs;
using ApprovalService.Application.Mappings;
using ApprovalService.Domain.Enums;
using ApprovalService.Domain.Repositories;
using MediatR;

namespace ApprovalService.Application.Features.Queries
{
    /// <summary>Latest decided requests for an approvals tab plus both tab counts, with failed ones counted as approved.</summary>
    public class GetDecidedRequests
    {
        public record Query(bool? Approved, int Take) : IRequest<Result>;

        public record Result(IReadOnlyList<ApprovalRequestDto> Items, int ApprovedCount, int RejectedCount);

        private static readonly ApprovalStatus[] ApprovedStatuses = [ApprovalStatus.Approved, ApprovalStatus.Executed, ApprovalStatus.Failed];
        private static readonly ApprovalStatus[] RejectedStatuses = [ApprovalStatus.Rejected];

        public class Handler : IRequestHandler<Query, Result>
        {
            private readonly IApprovalRequestRepository _repository;

            public Handler(IApprovalRequestRepository repository)
            {
                _repository = repository;
            }

            public async Task<Result> Handle(Query request, CancellationToken cancellationToken)
            {
                // A null Approved asks only for the counts
                var items = request.Approved is bool approved
                    ? (await _repository.GetDecidedAsync(approved ? ApprovedStatuses : RejectedStatuses, request.Take, cancellationToken))
                        .Select(r => r.ToDto()).ToList()
                    : [];

                var approvedCount = 0;
                foreach (var status in ApprovedStatuses)
                    approvedCount += await _repository.CountAsync(status, cancellationToken: cancellationToken);
                var rejectedCount = await _repository.CountAsync(ApprovalStatus.Rejected, cancellationToken: cancellationToken);

                return new Result(items, approvedCount, rejectedCount);
            }
        }
    }
}
