using AutoMapper;
using MediatR;
using RouteService.Application.DTOs;
using RouteService.Domain.Repositories;

namespace RouteService.Application.Features.Routes.Queries
{
    public record GetAllRoutesQuery(
        int? PageNumber = 1,
        int? PageSize = 30,
        string? search=null,
        bool? IsCompleted = null,
        DateTime? StartDate = null,
        DateTime? EndDate = null) : IRequest<PagedResultDto<InventoryRouteDto>>;

    public class GetAllRoutesHandler : IRequestHandler<GetAllRoutesQuery, PagedResultDto<InventoryRouteDto>>
    {
        private readonly IInventoryRouteRepository _repository;
        private readonly IMapper _mapper;

        public GetAllRoutesHandler(IInventoryRouteRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
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
                cancellationToken);

            return new PagedResultDto<InventoryRouteDto>
            {
                Items = _mapper.Map<IEnumerable<InventoryRouteDto>>(routes.Items),
                TotalCount = routes.TotalCount,
                PageNumber = routes.PageNumber,
                PageSize = routes.PageSize
            };
        }
    }
}