using MediatR;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Features.Products.Commands
{
    public static class UpdateProductAfterRouting
    {
        public record Command(int ProductId, int DepartmentId, string? Worker) : IRequest;

        public class Handler : IRequestHandler<Command>
        {
            private readonly IProductRepository _productRepository;
            private readonly IUnitOfWork _unitOfWork;

            public Handler(IProductRepository productRepository,IUnitOfWork unitOfWork)
            {
                _productRepository = productRepository;
                _unitOfWork = unitOfWork;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken) 
                    ?? throw new ArgumentException($"Product with ID {request.ProductId} not found");

                product.UpdateAfterRouting(request.DepartmentId, request.Worker);

                await _productRepository.UpdateAsync(product, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
