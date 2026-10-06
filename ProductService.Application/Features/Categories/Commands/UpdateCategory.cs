using FluentValidation;
using MediatR;
using ProductService.Application.DTOs;
using ProductService.Application.Features.Lookups;
using ProductService.Domain.Repositories;
using SharedServices.Exceptions;

namespace ProductService.Application.Features.Categories.Commands
{
    public class UpdateCategory
    {
        /// <summary>With <paramref name="ClearBlankFields"/> a blank description clears the stored one instead of keeping it.</summary>
        public record Command(int Id, UpdateCategoryDto CategoryDto, bool ClearBlankFields = false) : IRequest;

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
                var dto = request.CategoryDto;
                // A category that already shares its name can still be edited, so only a new name is checked
                if (CatalogNames.IsRenamed(dto.Name, category.Name))
                    await _categoryRepository.EnsureNameIsFreeAsync(dto.Name, category.Id, cancellationToken);

                // A client that leaves the description out keeps it, so only a caller that sends every field can clear it
                var keepBlank = !request.ClearBlankFields;
                category.Update(
                    dto.Name,
                    keepBlank && string.IsNullOrWhiteSpace(dto.Description) ? category.Description : dto.Description,
                    dto.IsActive);

                await _categoryRepository.UpdateAsync(category, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }
}