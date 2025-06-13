using AutoMapper;
using MediatR;
using RouteService.Application.DTOs;
using RouteService.Domain.Repositories;

namespace RouteService.Application.Features.Routes.Queries
{
    public record GetRoutesByDepartmentQuery(int DepartmentId) : IRequest<IEnumerable<InventoryRouteDto>>;

    public class GetRoutesByDepartmentHandler : IRequestHandler<GetRoutesByDepartmentQuery, IEnumerable<InventoryRouteDto>>
    {
        private readonly IInventoryRouteRepository _repository;
        private readonly IMapper _mapper;

        public GetRoutesByDepartmentHandler(IInventoryRouteRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<InventoryRouteDto>> Handle(GetRoutesByDepartmentQuery request, CancellationToken cancellationToken)
        {
            var routes = await _repository.GetByDepartmentIdAsync(request.DepartmentId, cancellationToken);
            return _mapper.Map<IEnumerable<InventoryRouteDto>>(routes);
        }
    }
}