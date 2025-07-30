using FluentValidation;
using MediatR;
using ProductService.Application.DTOs;
using ProductService.Application.Events;
using ProductService.Application.Interfaces;
using ProductService.Domain.Entities;
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
            private readonly IMessagePublisher _messagePublisher;

            public UpdateProductCommandHandler(
                IProductRepository productRepository,
                ICategoryRepository categoryRepository,
                IDepartmentRepository departmentRepository,
                IUnitOfWork unitOfWork,
                IImageService imageService,
                ITransactionService transactionService,
                IMessagePublisher messagePublisher)
            {
                _productRepository = productRepository;
                _categoryRepository = categoryRepository;
                _departmentRepository = departmentRepository;
                _unitOfWork = unitOfWork;
                _imageService = imageService;
                _transactionService = transactionService;
                _messagePublisher = messagePublisher;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken) ??
                    throw new NotFoundException($"Product with ID {request.Id} not found");

                var dto = request.ProductDto;

                var existingProduct = new ExistingProduct
                {
                    ProductId = product.Id,
                    InventoryCode = product.InventoryCode,
                    CategoryId = product.CategoryId,
                    CategoryName = product.Category?.Name,
                    DepartmentId = product.DepartmentId,
                    DepartmentName = product.Department?.Name,
                    Worker = product.Worker,
                };

                // Track what changed
                var changes = new List<string>();

                if (product.Model != dto.Model)
                    changes.Add($"Model: {product.Model} → {dto.Model}");

                if (product.Vendor != dto.Vendor)
                    changes.Add($"Vendor: {product.Vendor} → {dto.Vendor}");

                if (product.Worker != dto.Worker)
                    changes.Add($"Worker: {product.Worker ?? "None"} → {dto.Worker ?? "None"}");

                if (product.CategoryId != dto.CategoryId)
                    changes.Add($"Category: {product.Category?.Name} → {await GetCategoryNameAsync(dto.CategoryId)}");

                if (product.DepartmentId != dto.DepartmentId)
                    changes.Add($"Department: {product.Department?.Name} → {await GetDepartmentNameAsync(dto.DepartmentId)}");

                if (dto.ImageFile != null)
                    changes.Add($"Image uploaded");


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
                        if (shouldUpdateImage && !string.IsNullOrEmpty(oldImageUrl))
                        {
                            await _imageService.DeleteImageAsync(oldImageUrl);
                        }

                        // Publish update event
                        if (changes.Any())
                        {
                            var updateEvent = new ProductUpdatedEvent
                            {
                                Product = existingProduct,
                                Changes = string.Join(", ", changes),
                                UpdatedAt = DateTime.UtcNow,
                            };

                            await _messagePublisher.PublishAsync(updateEvent, "product.updated", cancellationToken);
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


            private async Task<string?> GetCategoryNameAsync(int categoryId)
            {
                var categoryName = await _categoryRepository.GetByIdAsync(categoryId)
                    ?? throw new NotFoundException($"Category was not found with ID: {categoryId}");
                return categoryName?.Name;
            }
            private async Task<string?> GetDepartmentNameAsync(int departmentId)
            {
                var departmentName = await _departmentRepository.GetByIdAsync(departmentId)
                    ?? throw new NotFoundException($"Department was not found with ID: {departmentId}");
                return departmentName.Name;
            }
        }
    }
}