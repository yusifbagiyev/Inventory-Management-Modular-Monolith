using ApprovalService.Application.DTOs;
using ApprovalService.Domain.Repositories;
using AutoMapper;
using MediatR;

namespace ApprovalService.Application.Features.Queries
{
    public class GetAllRequests
    {
        public record Query : IRequest<IEnumerable<ApprovalRequestDto>>;

        public class Handler : IRequestHandler<Query, IEnumerable<ApprovalRequestDto>>
        {
            private readonly IApprovalRequestRepository _repository;
            private readonly IMapper _mapper;

            public Handler(IApprovalRequestRepository repository, IMapper mapper)
            {
                _repository = repository;
                _mapper = mapper;
            }

            public async Task<IEnumerable<ApprovalRequestDto>> Handle(Query request, CancellationToken cancellationToken)
            {
                var requests = await _repository.GetAllAsync(cancellationToken);
                return _mapper.Map<IEnumerable<ApprovalRequestDto>>(requests);
            }
        }
    }
}