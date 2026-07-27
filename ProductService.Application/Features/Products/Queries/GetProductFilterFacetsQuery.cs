using MediatR;
using ProductService.Application.DTOs;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Features.Products.Queries
{
    /// <summary>
    /// Returns the (department, category) pairs present in the inventory, so the list filters can
    /// cascade. The state/quick filters narrow the result, so the department and category options
    /// react to the other active filters. Cheap DISTINCT query; the unfiltered set is cached.
    /// </summary>
    public record GetProductFilterFacetsQuery(
        bool? Status = null,
        bool? Availability = null,
        bool? HasImage = null,
        bool? Assigned = null) : IRequest<ProductFilterFacetsDto>;

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
            var pairs = await _productRepository.GetDepartmentCategoryPairsAsync(
                request.Status, request.Availability, request.HasImage, request.Assigned, cancellationToken);

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
