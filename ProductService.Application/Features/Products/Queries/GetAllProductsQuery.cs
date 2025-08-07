using AutoMapper;
using MediatR;
using ProductService.Application.DTOs;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Features.Products.Queries
{
    public record GetAllProductsQuery(
        int? pageNumber =1,
        int? PageSize=30) : IRequest<PagedResultDto<ProductDto>>;

    public class GetAllProductsQueryHandler : IRequestHandler<GetAllProductsQuery, PagedResultDto<ProductDto>>
    {
        private readonly IProductRepository _productRepository;
        private readonly IMapper _mapper;

        public GetAllProductsQueryHandler(
            IProductRepository productRepository,
            IMapper mapper)
        {
            _productRepository = productRepository;
            _mapper = mapper;
        }

        public async Task<PagedResultDto<ProductDto>> Handle(GetAllProductsQuery request, CancellationToken cancellationToken)
        {
            var products = await _productRepository.GetAllAsync(
                request.pageNumber?? 1,
                request.PageSize ?? 30,
                cancellationToken);

            return new PagedResultDto<ProductDto>
            {
                Items = _mapper.Map<IEnumerable<ProductDto>>(products.Items),
                TotalCount = products.TotalCount,
                PageNumber = products.PageNumber,
                PageSize = products.PageSize
            };
        }
    }
}
