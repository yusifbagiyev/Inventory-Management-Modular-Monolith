using ApprovalService.Application.Events;
using ApprovalService.Application.Interfaces;
using ApprovalService.Domain.Repositories;
using MediatR;

namespace ApprovalService.Application.Features.Commands
{
    public class RejectRequest
    {
        public record Command(int RequestId, int UserId, string UserName, string Reason) : IRequest<bool>;

        public class Handler : IRequestHandler<Command, bool>
        {
            private readonly IApprovalRequestRepository _repository;
            private readonly IUnitOfWork _unitOfWork;
            private readonly IMessagePublisher _messagePublisher;

            public Handler(
                IApprovalRequestRepository repository,
                IUnitOfWork unitOfWork,
                IMessagePublisher messagePublisher)
            {
                _repository = repository;
                _unitOfWork = unitOfWork;
                _messagePublisher = messagePublisher;
            }

            public async Task<bool> Handle(Command request, CancellationToken cancellationToken)
            {
                var approvalRequest = await _repository.GetByIdAsync(request.RequestId, cancellationToken)
                    ?? throw new InvalidOperationException($"Request {request.RequestId} not found");

                approvalRequest.Reject(request.UserId, request.UserName, request.Reason);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var evt = new ApprovalRequestProcessedEvent
                {
                    RequestId = approvalRequest.Id,
                    RequestType = approvalRequest.RequestType,
                    Status = "Rejected",
                    ProcessedById = request.UserId,
                    ProcessedByName = request.UserName,
                    RequestedById = approvalRequest.RequestedById,
                    RejectionReason = request.Reason
                };

                await _messagePublisher.PublishAsync(evt, "approval.request.processed", cancellationToken);

                return true;
            }
        }
    }
}