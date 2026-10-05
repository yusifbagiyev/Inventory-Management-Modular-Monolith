using MediatR;
using ProductService.Application.DTOs;
using ProductService.Domain.Repositories;
using ProductService.Application.Mappings;

namespace ProductService.Application.Features.Categories.Queries
{
    public record GetPagedCategoriesQuery(
        int? pageNumber=1,
        int? pageSize=20,
        string? search=null,
        CatalogListFilter? filter=null) : IRequest<PagedResultDto<CategoryDto>>;

    public class  GetPagedCategoriesQueryHandler :IRequestHandler<GetPagedCategoriesQuery, PagedResultDto<CategoryDto>>
    {
        private readonly ICategoryRepository _categoryRepository;
        public GetPagedCategoriesQueryHandler(ICategoryRepository categoryRepository )
        {
            _categoryRepository= categoryRepository;
        }

        public async Task<PagedResultDto<CategoryDto>> Handle(GetPagedCategoriesQuery request, CancellationToken cancellationToken)
        {
            // Column filters and sorts need every row's counts, which is cheap for a list of this size
            if (request.filter != null)
            {
                var all = await (await _categoryRepository.GetAllAsync(cancellationToken)).ToDtosAsync(_categoryRepository, cancellationToken);
                return CatalogListing.Page(all, request.search, request.filter, request.pageNumber ?? 1, request.pageSize ?? 20);
            }

            var categories = await _categoryRepository.GetPagedAsync(
                request.pageNumber ?? 1,
                request.pageSize ?? 20,
                request.search,
                cancellationToken);

            return new PagedResultDto<CategoryDto>
            {
                Items = await categories.Items.ToDtosAsync(_categoryRepository, cancellationToken),
                TotalCount = categories.TotalCount,
                PageNumber = categories.PageNumber,
                PageSize = categories.PageSize
            };
        }
    }
}