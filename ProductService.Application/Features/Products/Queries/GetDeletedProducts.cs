using MediatR;
using ProductService.Application.DTOs;
using ProductService.Application.Mappings;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Features.Products.Queries
{
    /// <summary>Deleted products (soft delete), most recently deleted first.</summary>
    public record GetDeletedProductsQuery(string? Search = null, int PageNumber = 1, int PageSize = 30)
        : IRequest<PagedResultDto<ProductDto>>;

    /// <summary>One deleted product; null when it does not exist or is not deleted.</summary>
    public record GetDeletedProductByIdQuery(int Id) : IRequest<ProductDto?>;

    public class GetDeletedProductsHandler :
        IRequestHandler<GetDeletedProductsQuery, PagedResultDto<ProductDto>>,
        IRequestHandler<GetDeletedProductByIdQuery, ProductDto?>
    {
        private readonly IProductRepository _repository;

        public GetDeletedProductsHandler(IProductRepository repository) => _repository = repository;

        public async Task<PagedResultDto<ProductDto>> Handle(GetDeletedProductsQuery request, CancellationToken cancellationToken)
        {
            var pageSize = Math.Clamp(request.PageSize, 1, 200);
            var (items, total) = await _repository.GetDeletedAsync(request.Search, request.PageNumber, pageSize, cancellationToken);
            return new PagedResultDto<ProductDto>
            {
                Items = items.Select(p => p.ToDto()).ToList(),
                TotalCount = total,
                PageNumber = Math.Max(1, request.PageNumber),
                PageSize = pageSize
            };
        }

        public async Task<ProductDto?> Handle(GetDeletedProductByIdQuery request, CancellationToken cancellationToken)
            => (await _repository.GetDeletedByIdAsync(request.Id, cancellationToken))?.ToDto();
    }
}
