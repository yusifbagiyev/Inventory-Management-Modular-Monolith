using MediatR;
using RouteService.Application.DTOs;
using RouteService.Domain.Repositories;

namespace RouteService.Application.Features.Routes.Queries
{
    /// <summary>
    /// Returns the (department, category-name) pairs present across routes, so the route list filters
    /// can cascade. Cheap DISTINCT query.
    /// </summary>
    public record GetRouteFilterFacetsQuery() : IRequest<RouteFilterFacetsDto>;

    public class GetRouteFilterFacetsQueryHandler
        : IRequestHandler<GetRouteFilterFacetsQuery, RouteFilterFacetsDto>
    {
        private readonly IInventoryRouteRepository _repository;

        public GetRouteFilterFacetsQueryHandler(IInventoryRouteRepository repository)
        {
            _repository = repository;
        }

        public async Task<RouteFilterFacetsDto> Handle(
            GetRouteFilterFacetsQuery request, CancellationToken cancellationToken)
        {
            var pairs = await _repository.GetDepartmentCategoryPairsAsync(cancellationToken);

            return new RouteFilterFacetsDto
            {
                Pairs = pairs
                    .Select(p => new DepartmentCategoryNameFacetDto
                    {
                        DepartmentId = p.DepartmentId,
                        CategoryName = p.CategoryName
                    })
                    .ToList()
            };
        }
    }
}
