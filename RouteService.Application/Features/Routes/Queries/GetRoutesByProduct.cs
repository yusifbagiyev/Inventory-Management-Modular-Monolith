using MediatR;
using RouteService.Application.DTOs;
using RouteService.Domain.Repositories;
using RouteService.Application.Mappings;

namespace RouteService.Application.Features.Routes.Queries
{
    public record GetRoutesByProductQuery(int ProductId) : IRequest<IEnumerable<InventoryRouteDto>>;

    public class GetRoutesByProductHandler : IRequestHandler<GetRoutesByProductQuery, IEnumerable<InventoryRouteDto>>
    {
        private readonly IInventoryRouteRepository _repository;

        public GetRoutesByProductHandler(IInventoryRouteRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<InventoryRouteDto>> Handle(GetRoutesByProductQuery request, CancellationToken cancellationToken)
        {
            var routes = await _repository.GetByProductIdAsync(request.ProductId, cancellationToken);
            return routes.Select(x => x.ToDto());
        }
    }
}