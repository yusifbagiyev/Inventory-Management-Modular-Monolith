using FluentValidation;
using MediatR;
using ProductService.Domain.Exceptions;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Features.Products.Commands
{
    public class UpdateProductInventoryCode
    {
        public record Command(int Id, int InventoryCode) : IRequest;
        public class Validator:AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.InventoryCode)
                    .GreaterThan(0).WithMessage("Inventory code must be greater than 0")
                    .LessThan(9999).WithMessage("Inventory code must be less than 9999");
            }
        }
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
                var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
                if (product == null)
                {
                    throw new NotFoundException($"Product with ID {request.Id} not found");
                }
                product.ChangeInventoryCode(request.InventoryCode);
                await _productRepository.UpdateAsync(product, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
