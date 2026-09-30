using ApprovalService.Application.DTOs;
using ApprovalService.Domain.Repositories;
using MediatR;
using ApprovalService.Application.Mappings;

namespace ApprovalService.Application.Features.Queries
{
    public class GetPendingRequests
    {
        public record Query(int PageNumber, int PageSize) : IRequest<PagedResultDto<ApprovalRequestDto>>;

        public class Handler : IRequestHandler<Query, PagedResultDto<ApprovalRequestDto>>
        {
            private readonly IApprovalRequestRepository _repository;

            public Handler(IApprovalRequestRepository repository)
            {
                _repository = repository;
            }

            public async Task<PagedResultDto<ApprovalRequestDto>> Handle(Query request, CancellationToken cancellationToken)
            {
                var requests = await _repository.GetPendingAsync(request.PageNumber, request.PageSize, cancellationToken);
                var totalCount = await _repository.GetPendingCountAsync(cancellationToken);

                return new PagedResultDto<ApprovalRequestDto>
                {
                    Items = requests.Select(x => x.ToDto()),
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };
            }
        }
    }
}