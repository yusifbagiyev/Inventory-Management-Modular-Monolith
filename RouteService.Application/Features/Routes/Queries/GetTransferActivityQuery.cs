using MediatR;
using RouteService.Domain.Common;
using RouteService.Domain.Repositories;

namespace RouteService.Application.Features.Routes.Queries
{
    /// <summary>Transfers created between From and To, used by the dashboard.</summary>
    public record GetTransferActivityQuery(DateTime From, DateTime To) : IRequest<IReadOnlyList<TransferActivity>>;

    public class GetTransferActivityQueryHandler : IRequestHandler<GetTransferActivityQuery, IReadOnlyList<TransferActivity>>
    {
        private readonly IInventoryRouteRepository _repository;

        public GetTransferActivityQueryHandler(IInventoryRouteRepository repository)
        {
            _repository = repository;
        }

        public Task<IReadOnlyList<TransferActivity>> Handle(GetTransferActivityQuery request, CancellationToken cancellationToken)
            => _repository.GetTransferActivityAsync(request.From, request.To, cancellationToken);
    }
}
