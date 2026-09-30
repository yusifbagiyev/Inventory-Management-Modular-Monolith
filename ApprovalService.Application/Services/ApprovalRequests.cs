using ApprovalService.Application.Features.Commands;
using MediatR;
using SharedServices.Contracts;
using SharedServices.DTOs;

namespace ApprovalService.Application.Services
{
    /// <summary>The approval module's entry point for other modules.</summary>
    public class ApprovalRequests : IApprovalRequests
    {
        private readonly IMediator _mediator;

        public ApprovalRequests(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task<int> SubmitAsync(
            CreateApprovalRequestDto request,
            int requestedById,
            string requestedByName,
            CancellationToken cancellationToken = default)
        {
            var created = await _mediator.Send(
                new CreateApprovalRequest.Command(request, requestedById, requestedByName), cancellationToken);
            return created.Id;
        }
    }
}
