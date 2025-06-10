using MediatR;
using Microsoft.AspNetCore.Http;
using ProductService.Application.Interfaces;
using ProductService.Domain.Exceptions;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Features.Products.Commands
{
    public class UpdateProductImage
    {
        public record Command(int ProductId, IFormFile ImageFile) : IRequest;

        public class Handler : IRequestHandler<Command>
        {
            private readonly IProductRepository _productRepository;
            private readonly IImageService _imageService;
            private readonly IUnitOfWork _unitOfWork;

            public Handler(
                IProductRepository productRepository,
                IImageService imageService,
                IUnitOfWork unitOfWork)
            {
                _productRepository = productRepository;
                _imageService = imageService;
                _unitOfWork = unitOfWork;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken)
                    ?? throw new NotFoundException($"Product with ID {request.ProductId} not found");

                // Delete old image if exists
                if (!string.IsNullOrEmpty(product.ImageUrl))
                {
                    await _imageService.DeleteImageAsync(product.ImageUrl);
                }

                // Upload new image
                using var stream = request.ImageFile.OpenReadStream();
                var imageUrl = await _imageService.UploadImageAsync(
                    stream,
                    request.ImageFile.FileName,
                    product.InventoryCode);

                product.UpdateImage(imageUrl);
                await _productRepository.UpdateAsync(product, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }
}