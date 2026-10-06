using ApprovalService.Application.DTOs;
using ApprovalService.Domain.Repositories;
using MediatR;
using ApprovalService.Application.Mappings;

namespace ApprovalService.Application.Features.Queries
{
    /// <summary>One page of a user's own requests, newest first, with how many they have in each status.</summary>
    public class GetUserRequests
    {
        public const int DefaultPageSize = 100;

        public record Query(int UserId, int PageNumber = 1, int PageSize = DefaultPageSize) : IRequest<Result>;

        public record Result(IReadOnlyList<ApprovalRequestDto> Items, IReadOnlyDictionary<string, int> StatusCounts)
        {
            public int TotalCount => StatusCounts.Values.Sum();
        }

        public class Handler : IRequestHandler<Query, Result>
        {
            private readonly IApprovalRequestRepository _repository;

            public Handler(IApprovalRequestRepository repository)
            {
                _repository = repository;
            }

            public async Task<Result> Handle(Query request, CancellationToken cancellationToken)
            {
                var requests = await _repository.GetByUserIdAsync(request.UserId, request.PageNumber, request.PageSize, cancellationToken);
                var counts = await _repository.CountByStatusAsync(request.UserId, cancellationToken);
                return new Result(
                    requests.Select(x => x.ToDto()).ToList(),
                    counts.ToDictionary(c => c.Key.ToString(), c => c.Value));
            }
        }
    }
}
