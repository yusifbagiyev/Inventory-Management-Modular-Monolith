using ApprovalService.Application.DTOs;
using ApprovalService.Domain.Repositories;
using MediatR;
using ApprovalService.Application.Mappings;

namespace ApprovalService.Application.Features.Queries
{
    public class GetUserRequests
    {
        public record Query(int UserId) : IRequest<IEnumerable<ApprovalRequestDto>>;

        public class Handler : IRequestHandler<Query, IEnumerable<ApprovalRequestDto>>
        {
            private readonly IApprovalRequestRepository _repository;

            public Handler(IApprovalRequestRepository repository)
            {
                _repository = repository;
            }

            public async Task<IEnumerable<ApprovalRequestDto>> Handle(Query request, CancellationToken cancellationToken)
            {
                var requests = await _repository.GetByUserIdAsync(request.UserId, cancellationToken);
                return requests.Select(x => x.ToDto());
            }
        }
    }
}