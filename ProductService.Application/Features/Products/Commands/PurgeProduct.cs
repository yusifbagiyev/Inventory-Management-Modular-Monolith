using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ProductService.Domain.Repositories;
using SharedServices.Events;
using SharedServices.Exceptions;
using SharedServices.Persistence;
using SharedServices.Storage;

namespace ProductService.Application.Features.Products.Commands
{
    /// <summary>Removes a deleted product for good, for one created by mistake, so its code is free again and nothing of it stays.</summary>
    public class PurgeProduct
    {
        public record Command(int Id) : IRequest, ITransactionalRequest;

        public class Handler : IRequestHandler<Command>
        {
            private readonly IProductRepository _productRepository;
            private readonly IUnitOfWork _unitOfWork;
            private readonly IPublisher _publisher;
            private readonly DbSession _session;

            public Handler(IProductRepository productRepository, IUnitOfWork unitOfWork, IPublisher publisher, DbSession session)
            {
                _productRepository = productRepository;
                _unitOfWork = unitOfWork;
                _publisher = publisher;
                _session = session;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                // Only a deleted product, so a product in use is never removed in one step
                var product = await _productRepository.RemoveDeletedAsync(request.Id, cancellationToken)
                    ?? throw new NotFoundException($"Deleted product with ID {request.Id} not found");

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _publisher.Publish(new ProductPurgedEvent(product.Id, product.InventoryCode), cancellationToken);

                // The photos go only once the removal has committed
                foreach (var imageUrl in product.ImageUrls.Append(product.ImageUrl).Where(u => !string.IsNullOrEmpty(u)).Distinct())
                    _session.AfterCommit((services, _) => services.GetRequiredService<ImageStorage>().DeleteAsync(imageUrl));
            }
        }
    }
}
