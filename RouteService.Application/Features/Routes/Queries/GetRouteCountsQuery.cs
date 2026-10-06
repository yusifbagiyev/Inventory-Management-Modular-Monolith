using MediatR;
using RouteService.Domain.Common;
using RouteService.Domain.Repositories;

namespace RouteService.Application.Features.Routes.Queries
{
    /// <summary>Pending and completed counts under the list's filters, whatever completion state the list itself shows.</summary>
    public record GetRouteCountsQuery(
        string? Search = null,
        DateTime? StartDate = null,
        DateTime? EndDate = null,
        int? DepartmentId = null,
        string? DepartmentName = null,
        RouteListFilter? Filter = null) : IRequest<RouteCounts>;

    public record RouteCounts(int Pending, int Completed);

    public class GetRouteCountsHandler : IRequestHandler<GetRouteCountsQuery, RouteCounts>
    {
        private readonly IInventoryRouteRepository _repository;

        public GetRouteCountsHandler(IInventoryRouteRepository repository)
        {
            _repository = repository;
        }

        public async Task<RouteCounts> Handle(GetRouteCountsQuery request, CancellationToken cancellationToken)
        {
            var (pending, completed) = await _repository.CountByCompletionAsync(
                request.Search,
                request.StartDate,
                request.EndDate,
                request.DepartmentId,
                request.DepartmentName,
                request.Filter,
                cancellationToken);

            return new RouteCounts(pending, completed);
        }
    }
}
