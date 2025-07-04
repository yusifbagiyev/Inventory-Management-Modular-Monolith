using System.Text.Json;
using ApprovalService.Application.DTOs;
using ApprovalService.Domain.Entities;
using AutoMapper;
using MediatR;

namespace ApprovalService.Application.Features.Commands
{
    public class CreateApprovalRequest
    {
        public record Command(CreateApprovalRequestDto Dto, int UserId, string UserName) : IRequest<ApprovalRequestDto>;
        
        public class Handler : IRequestHandler<Command, ApprovalRequestDto>
        {
            private readonly IApprovalRequestRepository _repository;
            private readonly IUnitOfWork _unitOfWork;
            private readonly IMessagePublisher _messagePublisher;
            private readonly IMapper _mapper;
            
            public Handler(
                IApprovalRequestRepository repository,
                IUnitOfWork unitOfWork,
                IMessagePublisher messagePublisher,
                IMapper mapper)
            {
                _repository= repository;
                _unitOfWork= unitOfWork;
                _messagePublisher= messagePublisher;
                _mapper= mapper;
            }

            public async Task<ApprovalRequestDto> Handle(Command request, CancellationToken cancellationToken)
            {
                var actionDataJson = JsonSerializer.Serialize(request.Dto.ActionData);

                var approvalRequst = new ApprovalRequest(
                    request.Dto.RequestType,
                    request.Dto.EntityType,
                    request.Dto.EntityId,
                    actionDataJson,
                    request.UserId,
                    request.UserName);

                await _repository.AddAsync(approvalRequst, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                //Publish event for notification
                var evt = new ApprovalRequestCreatedEvent
                {
                    RequestId = approvalRequest.Id,
                    RequestType = approvalRequest.RequestType,
                    RequestedById = approvalRequest.RequestedById,
                    RequestedByName = approvalRequest.RequestedByName,
                    CreatedAt = approvalRequest.CreatedAt
                };

                await _messagePublisher.PublishAsync(evt, "approval.request.created", cancellationToken);

                return _mapper.Map<ApprovalRequestDto>(approvalRequst);
            }
        }
    }
}