using ApprovalService.Domain.Enums;
using ApprovalService.Domain.Repositories;
using MediatR;

namespace ApprovalService.Application.Features.Commands
{
    public class CancelRequest
    {
        public record Command(int RequestId) : IRequest;

        public class Handler : IRequestHandler<Command>
        {
            private readonly IApprovalRequestRepository _repository;
            private readonly IUnitOfWork _unitOfWork;

            public Handler(IApprovalRequestRepository repository, IUnitOfWork unitOfWork)
            {
                _repository = repository;
                _unitOfWork = unitOfWork;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var approvalRequest = await _repository.GetByIdAsync(request.RequestId, cancellationToken)
                    ?? throw new InvalidOperationException($"Request {request.RequestId} not found");

                if (approvalRequest.Status != ApprovalStatus.Pending)
                    throw new InvalidOperationException("Only pending requests can be cancelled");

                await _repository.DeleteAsync(approvalRequest, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }
}