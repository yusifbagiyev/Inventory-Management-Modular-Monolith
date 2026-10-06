using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ProductService.Application.DTOs;
using ProductService.Application.Interfaces;
using ProductService.Application.Mappings;
using ProductService.Domain.Repositories;
using SharedServices.Events;
using SharedServices.Exceptions;
using SharedServices.Persistence;
using SharedServices.Storage;

namespace ProductService.Application.Features.Products.Commands
{
    public class UpdateProduct
    {
        public record Command(int Id, UpdateProductDto ProductDto, List<string> Changes) : IRequest, ITransactionalRequest;

        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.ProductDto.CategoryId)
                    .GreaterThan(0).WithMessage("Valid category is required");

                RuleFor(x => x.ProductDto.DepartmentId)
                    .GreaterThan(0).WithMessage("Valid department is required");

                RuleFor(x => x.ProductDto.Model)
                    .MaximumLength(ProductDetails.MaxModelLength).WithMessage($"Model cannot exceed {ProductDetails.MaxModelLength} characters");

                RuleFor(x => x.ProductDto.Vendor)
                    .MaximumLength(ProductDetails.MaxVendorLength).WithMessage($"Vendor cannot exceed {ProductDetails.MaxVendorLength} characters");

                RuleFor(x => x.ProductDto.Worker)
                    .MaximumLength(ProductDetails.MaxWorkerLength).WithMessage($"Worker cannot exceed {ProductDetails.MaxWorkerLength} characters");

                RuleFor(x => x.ProductDto.Description)
                    .MaximumLength(ProductDetails.MaxDescriptionLength).WithMessage($"Description cannot exceed {ProductDetails.MaxDescriptionLength} characters");

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

        public class UpdateProductCommandHandler : IRequestHandler<Command>
        {
            private readonly IProductRepository _productRepository;
            private readonly IDepartmentRepository _departmentRepository;
            private readonly IUnitOfWork _unitOfWork;
            private readonly IImageService _imageService;
            private readonly IPublisher _publisher;
            private readonly DbSession _session;

            public UpdateProductCommandHandler(
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

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken)
                    ?? throw new NotFoundException($"Product with ID {request.Id} not found");

                var dto = request.ProductDto;
                // Moving into an inactive department is refused, but staying in one is fine
                if (dto.DepartmentId != product.DepartmentId)
                    await ProductDetails.RequireActiveDepartmentAsync(_departmentRepository, dto.DepartmentId, cancellationToken);
                var before = product.ToState();
                var oldCover = product.ImageUrl;

                var added = new List<string>();
                foreach (var file in ImageSet.Files(dto.ImageFile, dto.ImageFiles))
                {
                    await using var stream = file.OpenReadStream();
                    var uploaded = await _imageService.UploadImageAsync(stream, file.FileName, product.InventoryCode);
                    _session.OnRollback(() => _imageService.DeleteImageAsync(uploaded));
                    added.Add(uploaded);
                }

                // A single ImageFile from older clients replaces all images
                var (images, removed) = ImageSet.Apply(
                    product.ImageUrls, dto.RemoveImageUrls, added, ImageSet.ResolveCover(dto.CoverImageUrl, added),
                    replaceAll: dto.ImageFile is { Length: > 0 });
                if (!images.SequenceEqual(product.ImageUrls))
                    product.SetImages(images);

                product.Update(
                    dto.Model,
                    dto.Vendor,
                    dto.CategoryId,
                    dto.DepartmentId,
                    dto.Worker,
                    dto.Description,
                    dto.IsActive,
                    dto.IsNewItem,
                    dto.IsWorking);
                if (dto.ReplaceDetails)
                    product.SetDetails(dto.Color, ProductDetails.ToDomain(dto.Specifications));

                await _productRepository.UpdateAsync(product, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // Re-query so the category and department navigations follow the new ids
                var after = (await _productRepository.GetByIdAsync(product.Id, cancellationToken))!.ToState();

                await _publisher.Publish(new ProductUpdatedEvent(
                    before,
                    after,
                    ProductDetails.ChangeSummary(request.Changes),
                    // The history row keeps a copy of the cover when it changed
                    product.ImageUrl != oldCover && !string.IsNullOrEmpty(product.ImageUrl) ? product.ImageUrl : null,
                    DateTime.Now), cancellationToken);

                // Removed images are only deleted once the update is committed
                foreach (var url in removed)
                    _session.AfterCommit((sp, _) => sp.GetRequiredService<ImageStorage>().DeleteAsync(url));
            }
        }
    }
}
