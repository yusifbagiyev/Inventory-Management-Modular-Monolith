using MediatR;
using RouteService.Application.DTOs;
using RouteService.Domain.Enums;
using RouteService.Domain.Repositories;
using RouteService.Application.Mappings;

namespace RouteService.Application.Features.Routes.Queries
{
    public record GetAllRoutesQuery(
        int? PageNumber = 1,
        int? PageSize = 30,
        string? search=null,
        bool? IsCompleted = null,
        DateTime? StartDate = null,
        DateTime? EndDate = null,
        int? DepartmentId = null,
        string? CategoryName = null,
        RouteType? RouteType = null) : IRequest<PagedResultDto<InventoryRouteDto>>;

    public class GetAllRoutesHandler : IRequestHandler<GetAllRoutesQuery, PagedResultDto<InventoryRouteDto>>
    {
        private readonly IInventoryRouteRepository _repository;

        public GetAllRoutesHandler(IInventoryRouteRepository repository)
        {
            _repository = repository;
        }

        public async Task<PagedResultDto<InventoryRouteDto>> Handle(GetAllRoutesQuery request, CancellationToken cancellationToken)
        {
            var routes = await _repository.GetAllAsync(
                request.PageNumber ?? 1,
                request.PageSize ?? 30,
                request.search,
                request.IsCompleted,
                request.StartDate,
                request.EndDate,
                request.DepartmentId,
                request.CategoryName,
                request.RouteType,
                cancellationToken);

            return new PagedResultDto<InventoryRouteDto>
            {
                Items = routes.Items.Select(x => x.ToDto()),
                TotalCount = routes.TotalCount,
                PageNumber = routes.PageNumber,
                PageSize = routes.PageSize
            };
        }
    }
}