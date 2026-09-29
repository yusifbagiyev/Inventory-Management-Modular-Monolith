using MediatR;
using ProductService.Domain.Common;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Features.Lookups
{
    public record ProductLookupsDto(IReadOnlyList<LookupItem> Categories, IReadOnlyList<LookupItem> Departments);

    /// <summary>Category and department options for forms and filters - no product counts.</summary>
    public record GetLookupsQuery : IRequest<ProductLookupsDto>;

    public class GetLookupsQueryHandler : IRequestHandler<GetLookupsQuery, ProductLookupsDto>
    {
        private readonly ICategoryRepository _categories;
        private readonly IDepartmentRepository _departments;

        public GetLookupsQueryHandler(ICategoryRepository categories, IDepartmentRepository departments)
        {
            _categories = categories;
            _departments = departments;
        }

        public async Task<ProductLookupsDto> Handle(GetLookupsQuery request, CancellationToken cancellationToken)
            => new(await _categories.GetLookupAsync(cancellationToken), await _departments.GetLookupAsync(cancellationToken));
    }
}
