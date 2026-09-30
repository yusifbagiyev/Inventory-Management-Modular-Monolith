using FluentValidation;
using MediatR;
using ProductService.Application.DTOs;
using ProductService.Application.Interfaces;
using ProductService.Application.Mappings;
using ProductService.Domain.Entities;
using ProductService.Domain.Repositories;
using SharedServices.Events;
using SharedServices.Exceptions;
using SharedServices.Persistence;
using SharedServices.Storage;

namespace ProductService.Application.Features.Products.Commands
{
    public class CreateProduct
    {
        public record Command(CreateProductDto ProductDto) : IRequest<ProductDto>, ITransactionalRequest;

        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.ProductDto.InventoryCode)
                    .GreaterThan(0).WithMessage("Inventory code must be greater than 0")
                    .LessThan(10000).WithMessage("Inventory code must be less than 10000");

                RuleFor(x => x.ProductDto.CategoryId)
                    .GreaterThan(0).WithMessage("Valid category is required");

                RuleFor(x => x.ProductDto.DepartmentId)
                    .GreaterThan(0).WithMessage("Valid department is required");

                RuleFor(x => x.ProductDto.Color)
                    .MaximumLength(30).WithMessage("Color cannot exceed 30 characters");

                RuleFor(x => x.ProductDto.Specifications)
                    .Must(s => s == null || s.Count <= ProductDetails.MaxSpecifications)
                    .WithMessage($"A product can have at most {ProductDetails.MaxSpecifications} specifications");

                RuleForEach(x => x.ProductDto.Specifications).ChildRules(line =>
                {
                    line.RuleFor(s => s.Name).MaximumLength(50).WithMessage("A specification name cannot exceed 50 characters");
                    line.RuleFor(s => s.Value).MaximumLength(200).WithMessage("A specification value cannot exceed 200 characters");
                });

                RuleFor(x => ImageSet.Files(x.ProductDto.ImageFile, x.ProductDto.ImageFiles).Count)
                    .LessThanOrEqualTo(ImageSet.MaxImages).WithMessage($"An item can have at most {ImageSet.MaxImages} images");
            }
        }

        public class CreateProductCommandHandler : IRequestHandler<Command, ProductDto>
        {
            private readonly IProductRepository _productRepository;
            private readonly IDepartmentRepository _departmentRepository;
            private readonly IUnitOfWork _unitOfWork;
            private readonly IImageService _imageService;
            private readonly IPublisher _publisher;
            private readonly DbSession _session;

            public CreateProductCommandHandler(
                IProductRepository productRepository,
                IDepartmentRepository departmentRepository,
                IUnitOfWork unitOfWork,
                IImageService imageService,
                IPublisher publisher,
                DbSession session)
            {
                _productRepository = productRepository;
                _departmentRepository = departmentRepository;
                _unitOfWork = unitOfWork;
                _imageService = imageService;
                _publisher = publisher;
                _session = session;
            }

            public async Task<ProductDto> Handle(Command request, CancellationToken cancellationToken)
            {
                var dto = request.ProductDto;

                if (await _productRepository.GetByInventoryCodeAsync(dto.InventoryCode, cancellationToken) != null)
                    throw new DuplicateEntityException($"Product with inventory code {dto.InventoryCode} already exists");
                await ProductDetails.RequireActiveDepartmentAsync(_departmentRepository, dto.DepartmentId, cancellationToken);

                var imageUrls = new List<string>();
                foreach (var file in ImageSet.Files(dto.ImageFile, dto.ImageFiles))
                {
                    await using var stream = file.OpenReadStream();
                    var uploaded = await _imageService.UploadImageAsync(stream, file.FileName, dto.InventoryCode);
                    _session.OnRollback(() => _imageService.DeleteImageAsync(uploaded));
                    imageUrls.Add(uploaded);
                }

                var product = new Product(
                    dto.InventoryCode,
                    dto.Model,
                    dto.Vendor,
                    dto.CategoryId,
                    dto.DepartmentId,
                    dto.Worker,
                    imageUrls,
                    dto.Description,
                    dto.IsActive,
                    dto.IsWorking,
                    dto.IsNewItem);
                product.SetDetails(dto.Color, ProductDetails.ToDomain(dto.Specifications));

                await _productRepository.AddAsync(product, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // Reload to get Category/Department for the event and the response.
                var created = await _productRepository.GetByIdAsync(product.Id, cancellationToken)
                    ?? throw new NotFoundException($"Product with ID {product.Id} not found");

                await _publisher.Publish(new ProductCreatedEvent(created.ToState(), created.CreatedAt), cancellationToken);

                return created.ToDto();
            }
        }
    }
}
