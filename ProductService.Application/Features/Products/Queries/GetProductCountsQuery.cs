using MediatR;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Features.Products.Queries
{
    public record ProductCountsDto(int Total, int Active);

    /// <summary>Product counts, optionally restricted to products created in a date range.</summary>
    public record GetProductCountsQuery(DateTime? CreatedFrom = null, DateTime? CreatedTo = null) : IRequest<ProductCountsDto>;

    public class GetProductCountsQueryHandler : IRequestHandler<GetProductCountsQuery, ProductCountsDto>
    {
        private readonly IProductRepository _repository;

        public GetProductCountsQueryHandler(IProductRepository repository)
        {
            _repository = repository;
        }

        public async Task<ProductCountsDto> Handle(GetProductCountsQuery request, CancellationToken cancellationToken)
        {
            var (total, active) = await _repository.CountCreatedAsync(request.CreatedFrom, request.CreatedTo, cancellationToken);
            return new ProductCountsDto(total, active);
        }
    }
}
