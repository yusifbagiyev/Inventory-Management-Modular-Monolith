using FluentValidation;
using MediatR;
using ProductService.Application.DTOs;
using ProductService.Application.Interfaces;
using ProductService.Domain.Exceptions;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Features.Products.Commands
{
    public class UpdateProduct
    {
        public record Command(int Id, UpdateProductDto ProductDto) : IRequest;

        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.ProductDto.CategoryId)
                    .GreaterThan(0).WithMessage("Valid category is required");

                RuleFor(x => x.ProductDto.DepartmentId)
                    .GreaterThan(0).WithMessage("Valid department is required");

                RuleFor(x => x.ProductDto.Description)
                    .MaximumLength(500).WithMessage("Description cannot exceed 500 characters");
            }
        }

        public class UpdateProductCommandHandler : IRequestHandler<Command>
        {
            private readonly IProductRepository _productRepository;
            private readonly ICategoryRepository _categoryRepository;
            private readonly IDepartmentRepository _departmentRepository;
            private readonly IUnitOfWork _unitOfWork;
            private readonly IImageService _imageService;
            private readonly ITransactionService _transactionService;

            public UpdateProductCommandHandler(
                IProductRepository productRepository,
                ICategoryRepository categoryRepository,
                IDepartmentRepository departmentRepository,
                IUnitOfWork unitOfWork,
                IImageService imageService,
                ITransactionService transactionService)
            {
                _productRepository = productRepository;
                _categoryRepository = categoryRepository;
                _departmentRepository = departmentRepository;
                _unitOfWork = unitOfWork;
                _imageService = imageService;
                _transactionService = transactionService;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken) ??
                    throw new NotFoundException($"Product with ID {request.Id} not found");

                var dto = request.ProductDto;

                if (!await _categoryRepository.ExistsByIdAsync(dto.CategoryId, cancellationToken))
                    throw new ArgumentException($"Category with ID {dto.CategoryId} not found");

                if (!await _departmentRepository.ExistsByIdAsync(dto.DepartmentId, cancellationToken))
                    throw new ArgumentException($"Department with ID {dto.DepartmentId} not found");

                var oldImageUrl = product.ImageUrl;
                var newImageUrl = product.ImageUrl;
                var shouldUpdateImage = dto.ImageFile != null && dto.ImageFile.Length > 0;
                await _transactionService.ExecuteAsync(
                    async () =>
                    {
                        if (shouldUpdateImage)
                        {
                            // Upload new image
                            using var stream = dto.ImageFile!.OpenReadStream();
                            newImageUrl = await _imageService.UploadImageAsync(stream, dto.ImageFile.FileName, product.InventoryCode);
                        }
                        // Update product with new image URL or keep the old one
                        product.Update(
                            dto.Model,
                            dto.Vendor,
                            dto.CategoryId,
                            dto.DepartmentId,
                            dto.Worker,
                            shouldUpdateImage ? newImageUrl : oldImageUrl,
                            dto.Description);

                        await _productRepository.UpdateAsync(product, cancellationToken);
                        await _unitOfWork.SaveChangesAsync(cancellationToken);

                        // Delete old image only after successful update
                        if(shouldUpdateImage&& !string.IsNullOrEmpty(oldImageUrl))
                        {
                            await _imageService.DeleteImageAsync(oldImageUrl);
                        }
                        return Task.CompletedTask;
                    },
                    async () =>
                    {
                        // Delete new image if update fails
                        if (!string.IsNullOrEmpty(newImageUrl))
                        {
                            await _imageService.DeleteImageAsync(newImageUrl);
                        }
                    });
                
            }
        }
    }
}