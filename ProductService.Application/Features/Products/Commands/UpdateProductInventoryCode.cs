using FluentValidation;
using MediatR;
using ProductService.Application.Mappings;
using ProductService.Domain.Repositories;
using SharedServices.Events;
using SharedServices.Exceptions;
using SharedServices.Persistence;

namespace ProductService.Application.Features.Products.Commands
{
    public class UpdateProductInventoryCode
    {
        public record Command(int Id, int InventoryCode) : IRequest, ITransactionalRequest;

        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                // Same range as CreateProduct.
                RuleFor(x => x.InventoryCode)
                    .GreaterThan(0).WithMessage("Inventory code must be greater than 0")
                    .LessThan(10000).WithMessage("Inventory code must be less than 10000");
            }
        }

        public class Handler : IRequestHandler<Command>
        {
            private readonly IProductRepository _productRepository;
            private readonly IUnitOfWork _unitOfWork;
            private readonly IPublisher _publisher;

            public Handler(IProductRepository productRepository, IUnitOfWork unitOfWork, IPublisher publisher)
            {
                _productRepository = productRepository;
                _unitOfWork = unitOfWork;
                _publisher = publisher;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken)
                    ?? throw new NotFoundException($"Product with ID {request.Id} not found");

                if (product.InventoryCode == request.InventoryCode)
                    return;

                var existing = await _productRepository.GetByInventoryCodeAsync(request.InventoryCode, cancellationToken);
                if (existing != null && existing.Id != product.Id)
                    throw new DuplicateEntityException($"Inventory code {request.InventoryCode} already exists");

                var before = product.ToState();
                product.ChangeInventoryCode(request.InventoryCode);

                await _productRepository.UpdateAsync(product, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // Previously this event was only sent when the code did NOT change, so real changes
                // never reached the route history.
                await _publisher.Publish(new ProductUpdatedEvent(
                    before,
                    product.ToState(),
                    $"Inventory code changed from {before.InventoryCode} to {request.InventoryCode}",
                    NewImageUrl: null,
                    DateTime.Now), cancellationToken);
            }
        }
    }
}
