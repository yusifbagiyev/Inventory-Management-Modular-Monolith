using MediatR;
using ProductService.Application.DTOs;
using ProductService.Domain.Repositories;
using ProductService.Application.Mappings;

namespace ProductService.Application.Features.Categories.Queries
{
    public record GetCategoryByIdQuery(int Id) : IRequest<CategoryDto?>;

    public class GetCategoryByIdQueryHandler : IRequestHandler<GetCategoryByIdQuery, CategoryDto?>
    {
        private readonly ICategoryRepository _categoryRepository;

        public GetCategoryByIdQueryHandler(ICategoryRepository categoryRepository)
        {
            _categoryRepository = categoryRepository;
        }

        public async Task<CategoryDto?> Handle(GetCategoryByIdQuery request, CancellationToken cancellationToken)
        {
            var category = await _categoryRepository.GetByIdAsync(request.Id, cancellationToken);
            if (category == null) return null;

            return (await new[] { category }.ToDtosAsync(_categoryRepository, cancellationToken))[0];
        }
    }
}
