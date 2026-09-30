using ApprovalService.Application.DTOs;
using ApprovalService.Domain.Repositories;
using MediatR;
using ApprovalService.Application.Mappings;

namespace ApprovalService.Application.Features.Queries
{
    public class GetRequestById
    {
        public record Query(int Id) : IRequest<ApprovalRequestDto?>;

        public class Handler : IRequestHandler<Query,ApprovalRequestDto?>
        {
            private readonly IApprovalRequestRepository _repository;
            public Handler(IApprovalRequestRepository repository)
            {
                _repository=repository;
            }
            public async Task<ApprovalRequestDto?> Handle(Query request,CancellationToken cancellationToken)
            {
                var approvalRequest= await _repository.GetByIdAsync(request.Id,cancellationToken);
                if (approvalRequest == null) return null;
                return approvalRequest.ToDto();
            }
        }
    }
}