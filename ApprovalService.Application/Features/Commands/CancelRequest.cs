using ApprovalService.Domain.Enums;
using ApprovalService.Domain.Repositories;
using MediatR;
using SharedServices.Events;
using SharedServices.Exceptions;
using SharedServices.Persistence;

namespace ApprovalService.Application.Features.Commands
{
    public class CancelRequest
    {
        /// <param name="UserId">The caller, who must be the requester.</param>
        public record Command(int RequestId, int UserId) : IRequest, ITransactionalRequest;

        public class Handler : IRequestHandler<Command>
        {
            private readonly IApprovalRequestRepository _repository;
            private readonly IPublisher _publisher;
            private readonly IUnitOfWork _unitOfWork;

            public Handler(IApprovalRequestRepository repository, IPublisher publisher, IUnitOfWork unitOfWork)
            {
                _repository = repository;
                _publisher = publisher;
                _unitOfWork = unitOfWork;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var approvalRequest = await _repository.GetByIdAsync(request.RequestId, cancellationToken)
                    ?? throw new NotFoundException($"Request {request.RequestId} not found");

                if (approvalRequest.RequestedById != request.UserId)
                    throw new InsufficientPermissionsException("You can only cancel your own requests");

                if (approvalRequest.Status != ApprovalStatus.Pending)
                    throw new InvalidOperationException("Only pending requests can be cancelled");

                await _repository.DeleteAsync(approvalRequest, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _publisher.Publish(new ApprovalRequestCancelledEvent(
                    approvalRequest.Id,
                    approvalRequest.RequestType,
                    approvalRequest.RequestedById,
                    DateTime.Now), cancellationToken);
            }
        }
    }
}
