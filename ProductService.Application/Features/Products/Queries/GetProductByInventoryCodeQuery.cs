using MediatR;
using ProductService.Application.DTOs;
using ProductService.Domain.Repositories;
using ProductService.Application.Mappings;

namespace ProductService.Application.Features.Products.Queries
{
    public record GetProductByInventoryCodeQuery(int InventoryCode) : IRequest<ProductDto?>;
    public class GetProductByInventoryCodeHandler : IRequestHandler<GetProductByInventoryCodeQuery, ProductDto?>
    {
        private readonly IProductRepository _repository;
        public GetProductByInventoryCodeHandler(IProductRepository repository)
        {
            _repository = repository;
        }
        public async Task<ProductDto?> Handle(GetProductByInventoryCodeQuery request, CancellationToken cancellationToken)
        {
            var product = await _repository.GetByInventoryCodeAsync(request.InventoryCode, cancellationToken);
            return product == null ? null : product.ToDto();
        }
    }
}
