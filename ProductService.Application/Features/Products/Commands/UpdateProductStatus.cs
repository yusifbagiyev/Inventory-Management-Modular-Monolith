using MediatR;
using Microsoft.Extensions.Logging;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Features.Products.Commands
{
    public static class UpdateProductStatus
    {
        public record Command(int ProductId, bool IsActive) : IRequest;

        public class Handler : IRequestHandler<Command>
        {
            private readonly IProductRepository _productRepository;
            private readonly IUnitOfWork _unitOfWork;

            public Handler(IProductRepository productRepository, IUnitOfWork unitOfWork)
            {
                _productRepository = productRepository;
                _unitOfWork = unitOfWork;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken) 
                    ?? throw new ArgumentException($"Product with ID {request.ProductId} not found");

                product.SetActiveStatus(request.IsActive); 

                await _productRepository.UpdateAsync(product, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
