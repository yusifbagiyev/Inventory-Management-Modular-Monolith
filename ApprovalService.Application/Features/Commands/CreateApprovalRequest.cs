using System.Text.Json;
using ApprovalService.Application.DTOs;
using ApprovalService.Domain.Entities;
using ApprovalService.Domain.Repositories;
using MediatR;
using SharedServices.DTOs;
using SharedServices.Events;
using SharedServices.Persistence;
using ApprovalService.Application.Mappings;

namespace ApprovalService.Application.Features.Commands
{
    public class CreateApprovalRequest
    {
        public record Command(CreateApprovalRequestDto Dto, int UserId, string UserName)
            : IRequest<ApprovalRequestDto>, ITransactionalRequest;

        public class Handler : IRequestHandler<Command, ApprovalRequestDto>
        {
            private readonly IApprovalRequestRepository _repository;
            private readonly IUnitOfWork _unitOfWork;
            private readonly IPublisher _publisher;

            public Handler(
                IApprovalRequestRepository repository,
                IUnitOfWork unitOfWork,
                IPublisher publisher)
            {
                _repository = repository;
                _unitOfWork = unitOfWork;
                _publisher = publisher;
            }

            public async Task<ApprovalRequestDto> Handle(Command request, CancellationToken cancellationToken)
            {
                var approvalRequest = new ApprovalRequest(
                    request.Dto.RequestType,
                    request.Dto.EntityType,
                    request.Dto.EntityId,
                    JsonSerializer.Serialize(request.Dto.ActionData),
                    request.UserId,
                    request.UserName);

                await _repository.AddAsync(approvalRequest, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _publisher.Publish(new ApprovalRequestCreatedEvent(
                    approvalRequest.Id,
                    approvalRequest.RequestType,
                    approvalRequest.RequestedById,
                    approvalRequest.RequestedByName,
                    approvalRequest.CreatedAt), cancellationToken);

                return approvalRequest.ToDto();
            }
        }
    }
}
