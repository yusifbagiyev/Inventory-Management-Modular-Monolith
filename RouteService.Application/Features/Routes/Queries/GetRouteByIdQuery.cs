using MediatR;
using RouteService.Application.DTOs;
using RouteService.Domain.Repositories;
using RouteService.Application.Mappings;

namespace RouteService.Application.Features.Routes.Queries
{
    public record GetRouteByIdQuery(int Id) : IRequest<InventoryRouteDto?>;

    public class GetRouteByIdHandler : IRequestHandler<GetRouteByIdQuery, InventoryRouteDto?>
    {
        private readonly IInventoryRouteRepository _repository;

        public GetRouteByIdHandler(IInventoryRouteRepository repository)
        {
            _repository = repository;
        }

        public async Task<InventoryRouteDto?> Handle(GetRouteByIdQuery request, CancellationToken cancellationToken)
        {
            var route = await _repository.GetByIdAsync(request.Id, cancellationToken);
            return route == null ? null : route.ToDto();
        }
    }
}