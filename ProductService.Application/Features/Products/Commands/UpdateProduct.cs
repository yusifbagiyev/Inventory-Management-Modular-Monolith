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

                RuleFor(x => x.ProductDto.Description)
                    .MaximumLength(500).WithMessage("Description cannot exceed 500 characters");
            }
        }

        public class UpdateProductCommandHandler : IRequestHandler<Command>
        {
            private readonly IProductRepository _productRepository;
            private readonly IUnitOfWork _unitOfWork;
            private readonly IImageService _imageService;
            private readonly IPublisher _publisher;
            private readonly DbSession _session;

            public UpdateProductCommandHandler(
                IProductRepository productRepository,
                IUnitOfWork unitOfWork,
                IImageService imageService,
                IPublisher publisher,
                DbSession session)
            {
                _productRepository = productRepository;
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
                var before = product.ToState();
                var oldImageUrl = product.ImageUrl;

                string? newImageUrl = null;
                if (dto.ImageFile != null && dto.ImageFile.Length > 0)
                {
                    await using var stream = dto.ImageFile.OpenReadStream();
                    newImageUrl = await _imageService.UploadImageAsync(stream, dto.ImageFile.FileName, product.InventoryCode);
                    var uploaded = newImageUrl;
                    _session.OnRollback(() => _imageService.DeleteImageAsync(uploaded));
                }

                product.Update(
                    dto.Model,
                    dto.Vendor,
                    dto.CategoryId,
                    dto.DepartmentId,
                    dto.Worker,
                    newImageUrl ?? oldImageUrl,
                    dto.Description,
                    dto.IsActive,
                    dto.IsNewItem,
                    dto.IsWorking);

                await _productRepository.UpdateAsync(product, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // Re-query so the Category/Department navigations follow the new ids.
                var after = (await _productRepository.GetByIdAsync(product.Id, cancellationToken))!.ToState();

                await _publisher.Publish(new ProductUpdatedEvent(
                    before,
                    after,
                    string.Join(", ", request.Changes),
                    newImageUrl,
                    DateTime.Now), cancellationToken);

                // The replaced image is only removed once the update is durable.
                if (newImageUrl != null && !string.IsNullOrEmpty(oldImageUrl))
                    _session.AfterCommit((sp, _) => sp.GetRequiredService<ImageStorage>().DeleteAsync(oldImageUrl));
            }
        }
    }
}
