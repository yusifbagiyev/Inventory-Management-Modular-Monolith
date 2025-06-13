using AutoMapper;
using MediatR;
using RouteService.Application.DTOs;
using RouteService.Domain.Repositories;

namespace RouteService.Application.Features.Routes.Queries
{
    public record GetRouteByIdQuery(int Id) : IRequest<InventoryRouteDto?>;

    public class GetRouteByIdHandler : IRequestHandler<GetRouteByIdQuery, InventoryRouteDto?>
    {
        private readonly IInventoryRouteRepository _repository;
        private readonly IMapper _mapper;

        public GetRouteByIdHandler(IInventoryRouteRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public async Task<InventoryRouteDto?> Handle(GetRouteByIdQuery request, CancellationToken cancellationToken)
        {
            var route = await _repository.GetByIdAsync(request.Id, cancellationToken);
            return route == null ? null : _mapper.Map<InventoryRouteDto>(route);
        }
    }
}