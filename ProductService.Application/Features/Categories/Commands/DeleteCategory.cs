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
            private readonly IUnitOfWork _unitOfWork;

            public DeleteCategoryCommandHandler(ICategoryRepository categoryRepository, IUnitOfWork unitOfWork)
            {
                _categoryRepository = categoryRepository;
                _unitOfWork = unitOfWork;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var category = await _categoryRepository.GetByIdAsync(request.Id, cancellationToken) ??
                    throw new NotFoundException($"Category with ID {request.Id} not found");
                await _categoryRepository.DeleteAsync(category, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }
}