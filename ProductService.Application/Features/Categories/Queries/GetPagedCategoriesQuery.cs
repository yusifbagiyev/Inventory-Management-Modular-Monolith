using MediatR;
using ProductService.Application.DTOs;
using ProductService.Domain.Repositories;
using ProductService.Application.Mappings;

namespace ProductService.Application.Features.Categories.Queries
{
    public record GetPagedCategoriesQuery(
        int? pageNumber=1,
        int? pageSize=20,
        string? search=null) : IRequest<PagedResultDto<CategoryDto>>;

    public class  GetPagedCategoriesQueryHandler :IRequestHandler<GetPagedCategoriesQuery, PagedResultDto<CategoryDto>>
    {
        private readonly ICategoryRepository _categoryRepository;
        public GetPagedCategoriesQueryHandler(ICategoryRepository categoryRepository )
        {
            _categoryRepository= categoryRepository;
        }

        public async Task<PagedResultDto<CategoryDto>> Handle(GetPagedCategoriesQuery request, CancellationToken cancellationToken)
        {
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