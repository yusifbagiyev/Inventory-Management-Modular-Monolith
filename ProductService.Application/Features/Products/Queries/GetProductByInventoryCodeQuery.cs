using AutoMapper;
using MediatR;
using ProductService.Application.DTOs;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Features.Products.Queries
{
    public record GetProductByInventoryCodeQuery(int InventoryCode) : IRequest<ProductDto?>;
    public class GetProductByInventoryCodeHandler : IRequestHandler<GetProductByInventoryCodeQuery, ProductDto?>
    {
        private readonly IProductRepository _repository;
        private readonly IMapper _mapper;
        public GetProductByInventoryCodeHandler(IProductRepository repository,IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }
        public async Task<ProductDto?> Handle(GetProductByInventoryCodeQuery request, CancellationToken cancellationToken)
        {
            var product = await _repository.GetByInventoryCodeAsync(request.InventoryCode, cancellationToken);
            return product == null ? null : _mapper.Map<ProductDto>(product);
        }
    }
}
