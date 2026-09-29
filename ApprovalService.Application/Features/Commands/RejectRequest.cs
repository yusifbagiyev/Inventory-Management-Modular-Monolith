using ApprovalService.Domain.Repositories;
using MediatR;
using SharedServices.Events;
using SharedServices.Exceptions;
using SharedServices.Persistence;

namespace ApprovalService.Application.Features.Commands
{
    public class RejectRequest
    {
        public record Command(int RequestId, int UserId, string UserName, string Reason) : IRequest<bool>, ITransactionalRequest;

        public class Handler : IRequestHandler<Command, bool>
        {
            private readonly IApprovalRequestRepository _repository;
            private readonly IUnitOfWork _unitOfWork;
            private readonly IPublisher _publisher;

            public Handler(IApprovalRequestRepository repository, IUnitOfWork unitOfWork, IPublisher publisher)
            {
                _repository = repository;
                _unitOfWork = unitOfWork;
                _publisher = publisher;
            }

            public async Task<bool> Handle(Command request, CancellationToken cancellationToken)
            {
                var approvalRequest = await _repository.GetByIdAsync(request.RequestId, cancellationToken)
                    ?? throw new NotFoundException($"Request {request.RequestId} not found");

                // Throws unless the request is still pending.
                approvalRequest.Reject(request.UserId, request.UserName, request.Reason);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _publisher.Publish(new ApprovalRequestProcessedEvent(
                    approvalRequest.Id,
                    approvalRequest.RequestType,
                    "Rejected",
                    request.UserId,
                    request.UserName,
                    approvalRequest.RequestedById,
                    request.Reason), cancellationToken);

                return true;
            }
        }
    }
}
