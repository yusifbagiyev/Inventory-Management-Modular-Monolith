using MediatR;
using ProductService.Application.Events;
using ProductService.Application.Interfaces;
using SharedServices.Exceptions;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Features.Products.Commands
{
    public class DeleteProduct
    {
        public record Command(int Id,string? userName) : IRequest;
        public class DeleteProductCommandHandler : IRequestHandler<Command>
        {
            private readonly IProductRepository _productRepository;
            private readonly IUnitOfWork _unitOfWork;
            private readonly IImageService _imageService;
            private readonly IMessagePublisher _messagePublisher;

            public DeleteProductCommandHandler(
                IProductRepository productRepository,
                IUnitOfWork unitOfWork,
                IImageService ımageService,
                IMessagePublisher messagePublisher)
            {
                _productRepository = productRepository;
                _unitOfWork = unitOfWork;
                _imageService = ımageService;
                _messagePublisher = messagePublisher;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken) ??
                    throw new NotFoundException($"Product with ID {request.Id} not found");

                var deletedEvent = new ProductDeletedEvent
                {
                    ProductId = product.Id,
                    InventoryCode = product.InventoryCode,
                    Model = product.Model,
                    Vendor = product.Vendor,
                    Worker = product.Worker,
                    CategoryName = product.Category?.Name ?? "Unknown",
                    DepartmentName=product.Department?.Name?? "Unknown",
                    DepartmentId=product.DepartmentId,
                    IsWorking= product.IsWorking,
                    DeletedAt = DateTime.Now,
                    RemovedBy=request.userName ?? "Unknown"
                };
                await _messagePublisher.PublishAsync(deletedEvent, "product.deleted", cancellationToken);

                await _imageService.DeleteInventoryFolderAsync(product.InventoryCode);

                await _productRepository.DeleteAsync(product, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }
}