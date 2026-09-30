using ApprovalService.Domain.Enums;
using ApprovalService.Domain.Repositories;
using MediatR;

namespace ApprovalService.Application.Features.Queries
{
    public class GetApprovalStatistics
    {
        public record Result(int Pending, int ExecutedToday, int RejectedToday);

        public record Query : IRequest<Result>;

        public class Handler : IRequestHandler<Query, Result>
        {
            private readonly IApprovalRequestRepository _repository;

            public Handler(IApprovalRequestRepository repository)
            {
                _repository = repository;
            }

            public async Task<Result> Handle(Query request, CancellationToken cancellationToken)
            {
                var today = DateTime.Today;
                return new Result(
                    await _repository.CountAsync(ApprovalStatus.Pending, cancellationToken: cancellationToken),
                    await _repository.CountAsync(ApprovalStatus.Executed, today, cancellationToken),
                    await _repository.CountAsync(ApprovalStatus.Rejected, today, cancellationToken));
            }
        }
    }
}
