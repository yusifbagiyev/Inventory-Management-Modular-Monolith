using ApprovalService.Application.Interfaces;
using ApprovalService.Domain.Enums;
using ApprovalService.Domain.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using SharedServices.Contracts;
using SharedServices.Events;
using SharedServices.Exceptions;
using SharedServices.Persistence;
using SharedServices.Web;

namespace ApprovalService.Application.Features.Commands
{
    /// <summary>Approves a pending request and runs its action, recording the request as Failed if the action throws.</summary>
    public class ApproveRequest
    {
        /// <param name="ApproverIsAdmin">Only Admins may approve their own requests.</param>
        public record Command(int RequestId, int UserId, string UserName, bool ApproverIsAdmin = false) : IRequest<bool>, ITransactionalRequest;

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

                // Otherwise approval.decide plus the request permission would act as the direct permission
                if (approvalRequest.RequestedById == request.UserId && !request.ApproverIsAdmin)
                    throw new InsufficientPermissionsException("You cannot approve your own request. The decision must be made by another approver.");

                approvalRequest.Approve(request.UserId, request.UserName);
                await _repository.UpdateAsync(approvalRequest, cancellationToken);
                // The row version makes a concurrent second approval fail here, before the action runs twice
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // A failed action rolls back to here, so the request can still be saved as Failed in this transaction
                await _session.SavepointAsync(ExecutionSavepoint, cancellationToken);
                try
                {
                    await _actionExecutor.ExecuteAsync(
                        approvalRequest.RequestType,
                        approvalRequest.ActionData,
                        new ApprovalActor(request.UserName),
                        cancellationToken);

                    approvalRequest.MarkAsExecuted();
                    _logger.LogInformation("Request {RequestId} executed by admin {AdminName}",
                        request.RequestId, request.UserName);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    await _session.RollbackToSavepointAsync(ExecutionSavepoint, cancellationToken);
                    approvalRequest.MarkAsFailed(Truncate($"Execution error: {FailureMessage(ex)}", 500));
                    _logger.LogWarning(ex, "Request {RequestId} failed to execute for admin {AdminName}",
                        request.RequestId, request.UserName);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                // The uploads are stored with the record now, or the request has ended without them
                await _repository.DropImageDataAsync(approvalRequest.Id, cancellationToken);

                var executed = approvalRequest.Status == ApprovalStatus.Executed;
                await _publisher.Publish(new ApprovalRequestProcessedEvent(
                    approvalRequest.Id,
                    approvalRequest.RequestType,
                    executed ? "Approved" : "Failed",
                    request.UserName,
                    approvalRequest.RequestedById,
                    executed ? null : approvalRequest.RejectionReason), cancellationToken);

                return executed;
            }

            // The reason reaches the requester, so framework and file-system messages stay in the log
            private static string FailureMessage(Exception ex) => ex is ValidationException { Errors: var errors } && errors.Any()
                ? string.Join("; ", errors.Select(e => e.ErrorMessage).Distinct())
                : UserFacingErrors.MessageOf(ex);

            private static string Truncate(string value, int maxLength)
                => value.Length <= maxLength ? value : value[..maxLength];
        }
    }
}
