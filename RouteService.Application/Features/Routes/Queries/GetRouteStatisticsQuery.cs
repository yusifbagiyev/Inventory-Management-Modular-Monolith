using MediatR;
using RouteService.Application.DTOs;

namespace RouteService.Application.Features.Routes.Queries
{
    public record GetRouteStatisticsQuery(DateTime? StartDate, DateTime? EndDate) : IRequest<RouteStatisticsDto>;
}