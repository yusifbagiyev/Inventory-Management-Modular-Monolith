using ApprovalService.Application.DTOs;
using ApprovalService.Domain.Repositories;
using MediatR;
using ApprovalService.Application.Mappings;

namespace ApprovalService.Application.Features.Queries
{
    /// <summary>One page of every request, newest first, without the uploaded image bytes.</summary>
    public class GetAllRequests
    {
        public const int DefaultPageSize = 100;

        public record Query(int PageNumber = 1, int PageSize = DefaultPageSize) : IRequest<PagedResultDto<ApprovalRequestDto>>;

        public class Handler : IRequestHandler<Query, PagedResultDto<ApprovalRequestDto>>
        {
            private readonly IApprovalRequestRepository _repository;

            public Handler(IApprovalRequestRepository repository)
            {
                _repository = repository;
            }

            public async Task<PagedResultDto<ApprovalRequestDto>> Handle(Query request, CancellationToken cancellationToken)
            {
                var requests = await _repository.GetAllAsync(request.PageNumber, request.PageSize, cancellationToken);
                return new PagedResultDto<ApprovalRequestDto>
                {
                    Items = requests.Select(x => x.ToDto()).ToList(),
                    TotalCount = await _repository.CountAllAsync(cancellationToken),
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };
            }
        }
    }
}
