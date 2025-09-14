using AutoMapper;
using MediatR;
using ProductService.Application.DTOs;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Features.Categories.Queries
{
    public record GetPagedCategoriesQuery(
        int? pageNumber=1,
        int? pageSize=20,
        string? search=null) : IRequest<PagedResultDto<CategoryDto>>;

    public class  GetPagedCategoriesQueryHandler :IRequestHandler<GetPagedCategoriesQuery, PagedResultDto<CategoryDto>>
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IMapper _mapper;
        public GetPagedCategoriesQueryHandler(ICategoryRepository categoryRepository ,IMapper mapper)
        {
            _categoryRepository= categoryRepository;
            _mapper= mapper;
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
                Items = _mapper.Map<IEnumerable<CategoryDto>>(categories.Items),
                TotalCount = categories.TotalCount,
                PageNumber = categories.PageNumber,
                PageSize = categories.PageSize
            };
        }
    }
}