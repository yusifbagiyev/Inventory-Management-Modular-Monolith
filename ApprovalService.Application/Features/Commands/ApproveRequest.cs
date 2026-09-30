using ApprovalService.Application.Interfaces;
using ApprovalService.Domain.Enums;
using ApprovalService.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using SharedServices.Contracts;
using SharedServices.Events;
using SharedServices.Exceptions;
using SharedServices.Persistence;

namespace ApprovalService.Application.Features.Commands
{
    public class ApproveRequest
    {
        public record Command(int RequestId, int UserId, string UserName) : IRequest<bool>, ITransactionalRequest;

        public class Handler : IRequestHandler<Command, bool>
        {
            private const string ExecutionSavepoint = "approval_execution";

            private readonly IApprovalRequestRepository _repository;
            private readonly IUnitOfWork _unitOfWork;
            private readonly IActionExecutor _actionExecutor;
            private readonly IPublisher _publisher;
            private readonly DbSession _session;
            private readonly ILogger<Handler> _logger;

            public Handler(
                IApprovalRequestRepository repository,
                IUnitOfWork unitOfWork,
                IActionExecutor actionExecutor,
                IPublisher publisher,
                DbSession session,
                ILogger<Handler> logger)
            {
                _repository = repository;
                _unitOfWork = unitOfWork;
                _actionExecutor = actionExecutor;
                _publisher = publisher;
                _session = session;
                _logger = logger;
            }

            /// <returns>True when the approved action executed.</returns>
            public async Task<bool> Handle(Command request, CancellationToken cancellationToken)
            {
                var approvalRequest = await _repository.GetByIdAsync(request.RequestId, cancellationToken)
                    ?? throw new NotFoundException($"Request {request.RequestId} not found");

                if (approvalRequest.Status != ApprovalStatus.Pending)
                    throw new InvalidOperationException($"Request is no longer pending. Current status: {approvalRequest.Status}");

                approvalRequest.Approve(request.UserId, request.UserName);
                await _repository.UpdateAsync(approvalRequest, cancellationToken);
                // Row version check: a second admin approving concurrently fails here, before
                // the action could run twice.
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // The action runs in the same transaction behind a savepoint: on failure only its
                // own changes are undone and the request is recorded as Failed, instead of the old
                // behaviour of an "Approved" request whose action never ran.
                await _session.SavepointAsync(ExecutionSavepoint, cancellationToken);
                try
                {
                    await _actionExecutor.ExecuteAsync(
                        approvalRequest.RequestType,
                        approvalRequest.ActionData,
                        new ApprovalActor(request.UserId, request.UserName),
                        cancellationToken);

                    approvalRequest.MarkAsExecuted();
                    _logger.LogInformation("Request {RequestId} executed by admin {AdminName}",
                        request.RequestId, request.UserName);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    await _session.RollbackToSavepointAsync(ExecutionSavepoint, cancellationToken);
                    approvalRequest.MarkAsFailed(Truncate($"Execution error: {ex.Message}", 500));
                    _logger.LogWarning(ex, "Request {RequestId} failed to execute for admin {AdminName}",
                        request.RequestId, request.UserName);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var executed = approvalRequest.Status == ApprovalStatus.Executed;
                await _publisher.Publish(new ApprovalRequestProcessedEvent(
                    approvalRequest.Id,
                    approvalRequest.RequestType,
                    executed ? "Approved" : "Failed",
                    request.UserId,
                    request.UserName,
                    approvalRequest.RequestedById,
                    executed ? null : approvalRequest.RejectionReason), cancellationToken);

                return executed;
            }

            private static string Truncate(string value, int maxLength)
                => value.Length <= maxLength ? value : value[..maxLength];
        }
    }
}
