using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ProductService.Application.Mappings;
using ProductService.Domain.Repositories;
using SharedServices.Events;
using SharedServices.Exceptions;
using SharedServices.Persistence;
using SharedServices.Storage;

namespace ProductService.Application.Features.Products.Commands
{
    public class DeleteProduct
    {
        public record Command(int Id, string? UserName) : IRequest, ITransactionalRequest;

        public class DeleteProductCommandHandler : IRequestHandler<Command>
        {
            private readonly IProductRepository _productRepository;
            private readonly IUnitOfWork _unitOfWork;
            private readonly IPublisher _publisher;
            private readonly DbSession _session;

            public DeleteProductCommandHandler(
                IProductRepository productRepository,
                IUnitOfWork unitOfWork,
                IPublisher publisher,
                DbSession session)
            {
                _productRepository = productRepository;
                _unitOfWork = unitOfWork;
                _publisher = publisher;
                _session = session;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken)
                    ?? throw new NotFoundException($"Product with ID {request.Id} not found");

                var state = product.ToState();

                await _productRepository.DeleteAsync(product, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _publisher.Publish(new ProductDeletedEvent(state, request.UserName ?? "Unknown", DateTime.Now), cancellationToken);

                // Files go only after the delete is durable. The image may live in a folder named
                // after an earlier inventory code, so remove it by URL as well as the current folder.
                _session.AfterCommit(async (sp, _) =>
                {
                    var storage = sp.GetRequiredService<ImageStorage>();
                    await storage.DeleteAsync(state.ImageUrl);
                    await storage.DeleteFolderAsync(ImageStorage.Products, state.InventoryCode);
                });
            }
        }
    }
}
