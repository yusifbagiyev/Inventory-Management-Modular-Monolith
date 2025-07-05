using ApprovalService.Application.DTOs;
using ApprovalService.Domain.Repositories;
using AutoMapper;
using MediatR;

namespace ApprovalService.Application.Features.Queries
{
    public class GetPendingRequests
    {
        public record Query(int PageNumber, int PageSize) : IRequest<PagedResultDto<ApprovalRequestDto>>;

        public class Handler : IRequestHandler<Query, PagedResultDto<ApprovalRequestDto>>
        {
            private readonly IApprovalRequestRepository _repository;
            private readonly IMapper _mapper;

            public Handler(IApprovalRequestRepository repository, IMapper mapper)
            {
                _repository = repository;
                _mapper = mapper;
            }

            public async Task<PagedResultDto<ApprovalRequestDto>> Handle(Query request, CancellationToken cancellationToken)
            {
                var requests = await _repository.GetPendingAsync(request.PageNumber, request.PageSize, cancellationToken);
                var totalCount = await _repository.GetPendingCountAsync(cancellationToken);

                return new PagedResultDto<ApprovalRequestDto>
                {
                    Items = _mapper.Map<IEnumerable<ApprovalRequestDto>>(requests),
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };
            }
        }
    }
}