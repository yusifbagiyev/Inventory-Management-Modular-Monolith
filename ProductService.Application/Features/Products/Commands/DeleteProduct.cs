using MediatR;
using ProductService.Application.Interfaces;
using ProductService.Domain.Exceptions;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Features.Products.Commands
{
    public class DeleteProduct
    {
        public record Command(int Id) : IRequest;
        public class DeleteProductCommandHandler : IRequestHandler<Command>
        {
            private readonly IProductRepository _productRepository;
            private readonly IUnitOfWork _unitOfWork;
            private readonly IImageService _imageService;

            public DeleteProductCommandHandler(
                IProductRepository productRepository,
                IUnitOfWork unitOfWork,
                IImageService ımageService)
            {
                _productRepository = productRepository;
                _unitOfWork = unitOfWork;
                _imageService = ımageService;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken) ??
                    throw new NotFoundException($"Product with ID {request.Id} not found");

                //Delete image if exists
                if(!string.IsNullOrEmpty(product.ImageUrl))
                {
                    await _imageService.DeleteImageAsync(product.ImageUrl);
                }

                await _productRepository.DeleteAsync(product, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
