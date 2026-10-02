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

                // Never let a delete silently take products with it, same as for departments
                var productCount = await _productRepository.CountByCategoryIdAsync(request.Id, cancellationToken);
                if (productCount > 0)
                    throw new ConflictException(
                        $"Cannot delete category '{category.Name}': {productCount} product(s) are still assigned to it. Move them to another category first.");

                // Deleted products are kept and still point at their category
                var deletedCount = await _productRepository.CountDeletedByCategoryIdAsync(request.Id, cancellationToken);
                if (deletedCount > 0)
                    throw new ConflictException(
                        $"Cannot delete category '{category.Name}': {deletedCount} deleted product(s) still keep it in their record. Deactivate the category instead.");

                await _categoryRepository.DeleteAsync(category, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }
}