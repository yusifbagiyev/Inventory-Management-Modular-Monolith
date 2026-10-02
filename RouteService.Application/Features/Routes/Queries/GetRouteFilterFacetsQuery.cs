using MediatR;
using RouteService.Application.DTOs;
using RouteService.Domain.Enums;
using RouteService.Domain.Repositories;

namespace RouteService.Application.Features.Routes.Queries
{
    /// <summary>Department and category name pairs left by the status and type filters, so the list filters can cascade.</summary>
    public record GetRouteFilterFacetsQuery(
        bool? IsCompleted = null,
        RouteType? RouteType = null) : IRequest<RouteFilterFacetsDto>;

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
            var pairs = await _repository.GetDepartmentCategoryPairsAsync(
                request.IsCompleted, request.RouteType, cancellationToken);

            return new RouteFilterFacetsDto
            {
                Pairs = pairs
                    .Select(p => new DepartmentCategoryNameFacetDto
                    {
                        DepartmentName = p.DepartmentName,
                        CategoryName = p.CategoryName
                    })
                    .ToList()
            };
        }
    }
}
