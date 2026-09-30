using FluentValidation;
using MediatR;
using ProductService.Application.DTOs;
using ProductService.Domain.Entities;
using ProductService.Domain.Repositories;
using ProductService.Application.Mappings;

namespace ProductService.Application.Features.Categories.Commands
{
    public class CreateCategory
    {
        public record Command(CreateCategoryDto CategoryDto) : IRequest<CategoryDto>;
        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.CategoryDto.Name)
                    .NotEmpty().WithMessage("Name is required")
                    .MaximumLength(100).WithMessage("Name must not exceed 100 characters");

                RuleFor(x => x.CategoryDto.Description)
                    .MaximumLength(500).WithMessage("Description must not exceed 500 characters");
            }
        }
        public class CreateCategoryCommandHandler : IRequestHandler<Command, CategoryDto>
        {
            private readonly ICategoryRepository _categoryRepository;
            private readonly IUnitOfWork _unitOfWork;

            public CreateCategoryCommandHandler(
                ICategoryRepository categoryRepository,
                IUnitOfWork unitOfWork)
            {
                _categoryRepository = categoryRepository;
                _unitOfWork = unitOfWork;
            }

            public async Task<CategoryDto> Handle(Command request, CancellationToken cancellationToken)
            {
                var category = new Category(request.CategoryDto.Name, request.CategoryDto.Description,request.CategoryDto.IsActive);

                await _categoryRepository.AddAsync(category, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return category.ToDto(productCount: 0);
            }
        }
    }
}
