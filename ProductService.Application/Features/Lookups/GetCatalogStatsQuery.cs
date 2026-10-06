using MediatR;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Features.Lookups
{
    /// <summary>Active and inactive counts plus how many of them hold at least one product.</summary>
    public record CatalogStatsDto(int Active, int Inactive, int WithProducts);

    /// <summary>Header counters for the category list, computed with COUNT queries.</summary>
    public record GetCategoryStatsQuery : IRequest<CatalogStatsDto>;

    /// <summary>Header counters for the department list, computed with COUNT queries.</summary>
    public record GetDepartmentStatsQuery : IRequest<CatalogStatsDto>;

    public class GetCatalogStatsQueryHandler :
        IRequestHandler<GetCategoryStatsQuery, CatalogStatsDto>,
        IRequestHandler<GetDepartmentStatsQuery, CatalogStatsDto>
    {
        private readonly ICategoryRepository _categories;
        private readonly IDepartmentRepository _departments;

        public GetCatalogStatsQueryHandler(
            ICategoryRepository categories,
            IDepartmentRepository departments)
        {
            _categories = categories;
            _departments = departments;
        }

        public async Task<CatalogStatsDto> Handle(GetCategoryStatsQuery request, CancellationToken cancellationToken)
        {
            var (active, inactive) = await _categories.CountByActivityAsync(cancellationToken);
            return new(active, inactive, await _categories.CountWithProductsAsync(cancellationToken));
        }

        public async Task<CatalogStatsDto> Handle(GetDepartmentStatsQuery request, CancellationToken cancellationToken)
        {
            var (active, inactive) = await _departments.CountByActivityAsync(cancellationToken);
            return new(active, inactive, await _departments.CountWithProductsAsync(cancellationToken));
        }
    }
}
