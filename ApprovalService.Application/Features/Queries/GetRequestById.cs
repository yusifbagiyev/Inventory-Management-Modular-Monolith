using ApprovalService.Application.DTOs;
using ApprovalService.Domain.Repositories;
using AutoMapper;
using MediatR;

namespace ApprovalService.Application.Features.Queries
{
    public class GetRequestById
    {
        public record Query(int Id) : IRequest<ApprovalRequestDto?>;

        public class Handler : IRequestHandler<Query,ApprovalRequestDto?>
        {
            private readonly IApprovalRequestRepository _repository;
            private readonly IMapper _mapper;
            public Handler(IApprovalRequestRepository repository,
                IMapper mapper)
            {
                _repository=repository;
                _mapper=mapper;
            }
            public async Task<ApprovalRequestDto?> Handle(Query request,CancellationToken cancellationToken)
            {
                var approvalRequest= await _repository.GetByIdAsync(request.Id,cancellationToken);
                if (approvalRequest == null) return null;
                return _mapper.Map<ApprovalRequestDto>(approvalRequest);
            }
        }
    }
}