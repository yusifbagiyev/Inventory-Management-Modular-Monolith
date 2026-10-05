using MediatR;
using ProductService.Application.DTOs;
using ProductService.Domain.Common;
using ProductService.Domain.Repositories;
using ProductService.Application.Mappings;

namespace ProductService.Application.Features.Products.Queries
{
    public record GetAllProductsQuery(
        int? pageNumber =1,
        int? PageSize=30,
        string? search=null,
        DateTime? startDate=null,
        DateTime? endDate=null,
        bool? status=null,
        bool? availability=null,
        int? categoryId=null,
        int? departmentId=null,
        bool? hasImage=null,
        bool? assigned=null,
        ProductListFilter? Filter=null) : IRequest<PagedResultDto<ProductDto>>;

    public class GetAllProductsQueryHandler : IRequestHandler<GetAllProductsQuery, PagedResultDto<ProductDto>>
    {
        private readonly IProductRepository _productRepository;

        public GetAllProductsQueryHandler(
            IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        public async Task<PagedResultDto<ProductDto>> Handle(GetAllProductsQuery request, CancellationToken cancellationToken)
        {
            var products = await _productRepository.GetAllAsync(
                request.pageNumber?? 1,
                request.PageSize ?? 30,
                request.search,
                request.startDate,
                request.endDate,
                request.status,
                request.availability,
                request.categoryId,
                request.departmentId,
                request.hasImage,
                request.assigned,
                request.Filter,
                cancellationToken);

            return new PagedResultDto<ProductDto>
            {
                Items = products.Items.Select(x => x.ToDto()),
                TotalCount = products.TotalCount,
                PageNumber = products.PageNumber,
                PageSize = products.PageSize
            };
        }
    }
}