using AutoMapper;
using MediatR;
using RouteService.Application.DTOs;
using RouteService.Domain.Repositories;

namespace RouteService.Application.Features.Routes.Queries
{
    public record GetIncompleteRoutesQuery : IRequest<IEnumerable<InventoryRouteDto>>;

    public class GetIncompleteRoutesHandler : IRequestHandler<GetIncompleteRoutesQuery, IEnumerable<InventoryRouteDto>>
    {
        private readonly IInventoryRouteRepository _repository;
        private readonly IMapper _mapper;

        public GetIncompleteRoutesHandler(IInventoryRouteRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<InventoryRouteDto>> Handle(GetIncompleteRoutesQuery request, CancellationToken cancellationToken)
        {
            var routes = await _repository.GetIncompleteRoutesAsync(cancellationToken);
            return _mapper.Map<IEnumerable<InventoryRouteDto>>(routes);
        }
    }
}