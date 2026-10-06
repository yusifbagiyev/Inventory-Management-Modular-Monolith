using ApprovalService.Domain.Repositories;
using MediatR;

namespace ApprovalService.Application.Features.Queries
{
    /// <summary>How many requests wait for a decision, for the menu badge.</summary>
    public class GetPendingCount
    {
        public record Query : IRequest<int>;

        public class Handler : IRequestHandler<Query, int>
        {
            private readonly IApprovalRequestRepository _repository;

            public Handler(IApprovalRequestRepository repository)
            {
                _repository = repository;
            }

            public Task<int> Handle(Query request, CancellationToken cancellationToken)
                => _repository.GetPendingCountAsync(cancellationToken);
        }
    }
}
