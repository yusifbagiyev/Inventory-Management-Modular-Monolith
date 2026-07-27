using MediatR;
using ProductService.Application.DTOs;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Features.Products.Queries
{
    /// <summary>
    /// Returns the (department, category) pairs present in the inventory, so the list filters can
    /// cascade. Cheap DISTINCT query; the repository caches the result.
    /// </summary>
    public record GetProductFilterFacetsQuery() : IRequest<ProductFilterFacetsDto>;

    public class GetProductFilterFacetsQueryHandler
        : IRequestHandler<GetProductFilterFacetsQuery, ProductFilterFacetsDto>
    {
        private readonly IProductRepository _productRepository;

        public GetProductFilterFacetsQueryHandler(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        public async Task<ProductFilterFacetsDto> Handle(
            GetProductFilterFacetsQuery request, CancellationToken cancellationToken)
        {
            var pairs = await _productRepository.GetDepartmentCategoryPairsAsync(cancellationToken);

            return new ProductFilterFacetsDto
            {
                Pairs = pairs
                    .Select(p => new DepartmentCategoryFacetDto
                    {
                        DepartmentId = p.DepartmentId,
                        CategoryId = p.CategoryId
                    })
                    .ToList()
            };
        }
    }
}
