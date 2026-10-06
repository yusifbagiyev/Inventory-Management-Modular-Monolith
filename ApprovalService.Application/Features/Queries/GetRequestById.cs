using ApprovalService.Application.DTOs;
using ApprovalService.Domain.Repositories;
using MediatR;
using ApprovalService.Application.Mappings;

namespace ApprovalService.Application.Features.Queries
{
    public class GetRequestById
    {
        /// <param name="WithImageData">False leaves the uploaded image bytes in the database, which is all a page needs.</param>
        public record Query(int Id, bool WithImageData = true) : IRequest<ApprovalRequestDto?>;

        public class Handler : IRequestHandler<Query,ApprovalRequestDto?>
        {
            private readonly IApprovalRequestRepository _repository;
            public Handler(IApprovalRequestRepository repository)
            {
                _repository=repository;
            }
            public async Task<ApprovalRequestDto?> Handle(Query request,CancellationToken cancellationToken)
            {
                if (!request.WithImageData)
                    return (await _repository.GetSummaryByIdAsync(request.Id, cancellationToken))?.ToDto();

                var approvalRequest= await _repository.GetByIdAsync(request.Id,cancellationToken);
                if (approvalRequest == null) return null;
                return approvalRequest.ToDto();
            }
        }
    }
}
