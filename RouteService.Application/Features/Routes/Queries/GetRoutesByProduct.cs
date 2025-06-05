using AutoMapper;
using MediatR;
using RouteService.Application.DTOs;
using RouteService.Domain.Repositories;

namespace RouteService.Application.Features.Routes.Queries
{
    public record GetRoutesByProductQuery(int ProductId) : IRequest<IEnumerable<InventoryRouteDto>>;

    public class GetRoutesByProductHandler : IRequestHandler<GetRoutesByProductQuery, IEnumerable<InventoryRouteDto>>
    {
        private readonly IInventoryRouteRepository _repository;
        private readonly IMapper _mapper;

        public GetRoutesByProductHandler(IInventoryRouteRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<InventoryRouteDto>> Handle(GetRoutesByProductQuery request, CancellationToken cancellationToken)
        {
            var routes = await _repository.GetByProductIdAsync(request.ProductId, cancellationToken);
            return _mapper.Map<IEnumerable<InventoryRouteDto>>(routes);
        }
    }
}