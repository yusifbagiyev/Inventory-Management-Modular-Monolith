using FluentValidation;
using MediatR;
using ProductService.Application.DTOs;
using ProductService.Domain.Repositories;
using SharedServices.Exceptions;

namespace ProductService.Application.Features.Categories.Commands
{
    public class UpdateCategory
    {
        public record Command(int Id, UpdateCategoryDto CategoryDto) : IRequest;

        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.Id)
                .GreaterThan(0).WithMessage("Invalid category ID");

                RuleFor(x => x.CategoryDto.Name)
                    .NotEmpty().WithMessage("Name is required")
                    .MaximumLength(100).WithMessage("Name must not exceed 100 characters");

                RuleFor(x => x.CategoryDto.Description)
                    .MaximumLength(500).WithMessage("Description must not exceed 500 characters");
            }
        }
        public class UpdateCategoryCommandHandler : IRequestHandler<Command>
        {
            private readonly ICategoryRepository _categoryRepository;
            private readonly IUnitOfWork _unitOfWork;

            public UpdateCategoryCommandHandler(ICategoryRepository categoryRepository, IUnitOfWork unitOfWork)
            {
                _categoryRepository = categoryRepository;
                _unitOfWork = unitOfWork;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var category = await _categoryRepository.GetByIdAsync(request.Id, cancellationToken) ??
                    throw new NotFoundException($"Category with ID {request.Id} not found");
                category.Update(request.CategoryDto.Name, request.CategoryDto.Description,request.CategoryDto.IsActive);

                await _categoryRepository.UpdateAsync(category, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }
}