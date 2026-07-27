using MediatR;
using ProductService.Domain.Repositories;
using SharedServices.Exceptions;

namespace ProductService.Application.Features.Categories.Commands
{
    public class DeleteCategory
    {
        public record Command(int Id) : IRequest;

        public class DeleteCategoryCommandHandler : IRequestHandler<Command>
        {
            private readonly ICategoryRepository _categoryRepository;
            private readonly IProductRepository _productRepository;
            private readonly IUnitOfWork _unitOfWork;

            public DeleteCategoryCommandHandler(
                ICategoryRepository categoryRepository,
                IProductRepository productRepository,
                IUnitOfWork unitOfWork)
            {
                _categoryRepository = categoryRepository;
                _productRepository = productRepository;
                _unitOfWork = unitOfWork;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var category = await _categoryRepository.GetByIdAsync(request.Id, cancellationToken) ??
                    throw new NotFoundException($"Category with ID {request.Id} not found");

                // Same rule as departments: never let a delete silently take products with it.
                var productCount = await _productRepository.CountByCategoryIdAsync(request.Id, cancellationToken);
                if (productCount > 0)
                    throw new ConflictException(
                        $"Cannot delete category '{category.Name}': {productCount} product(s) are still assigned to it. Move them to another category first.");

                await _categoryRepository.DeleteAsync(category, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }
}