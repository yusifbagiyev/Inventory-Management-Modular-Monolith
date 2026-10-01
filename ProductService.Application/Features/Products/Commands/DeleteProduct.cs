using MediatR;
using ProductService.Application.Mappings;
using ProductService.Domain.Repositories;
using SharedServices.Events;
using SharedServices.Exceptions;
using SharedServices.Persistence;

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

            public DeleteProductCommandHandler(
                IProductRepository productRepository,
                IUnitOfWork unitOfWork,
                IPublisher publisher)
            {
                _productRepository = productRepository;
                _unitOfWork = unitOfWork;
                _publisher = publisher;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken)
                    ?? throw new NotFoundException($"Product with ID {request.Id} not found");

                var state = product.ToState();

                // Soft delete: the product is kept (with its images) for the Deleted products page;
                // the query filter hides it everywhere else and frees its inventory code.
                product.MarkDeleted(request.UserName);
                await _productRepository.UpdateAsync(product, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _publisher.Publish(new ProductDeletedEvent(state, request.UserName ?? "Unknown", DateTime.Now), cancellationToken);
            }
        }
    }
}
