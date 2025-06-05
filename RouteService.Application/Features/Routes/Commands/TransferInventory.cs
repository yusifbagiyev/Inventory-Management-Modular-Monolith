using AutoMapper;
using FluentValidation;
using MediatR;
using RouteService.Application.DTOs;
using RouteService.Application.DTOs.Commands;
using RouteService.Application.Events;
using RouteService.Application.Interfaces;
using RouteService.Domain.Entities;
using RouteService.Domain.Exceptions;
using RouteService.Domain.Repositories;
using RouteService.Domain.ValueObjects;

namespace RouteService.Application.Features.Routes.Commands
{
    public class TransferInventory
    {
        public record Command(TransferInventoryDto Dto) : IRequest<InventoryRouteDto>;

        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.Dto.ProductId).GreaterThan(0);
                RuleFor(x => x.Dto.ToDepartmentId).GreaterThan(0);
                RuleFor(x => x.Dto.FromWorker).NotEmpty().MaximumLength(100);
                RuleFor(x => x.Dto.ToWorker).NotEmpty().MaximumLength(100);
            }
        }

        public class Handler : IRequestHandler<Command, InventoryRouteDto>
        {
            private readonly IInventoryRouteRepository _repository;
            private readonly IProductServiceClient _productClient;
            private readonly IImageService _imageService;
            private readonly IMessagePublisher _messagePublisher;
            private readonly IUnitOfWork _unitOfWork;
            private readonly IMapper _mapper;

            public Handler(
                IInventoryRouteRepository repository,
                IProductServiceClient productClient,
                IImageService imageService,
                IMessagePublisher messagePublisher,
                IUnitOfWork unitOfWork,
                IMapper mapper)
            {
                _repository = repository;
                _productClient = productClient;
                _imageService = imageService;
                _messagePublisher = messagePublisher;
                _unitOfWork = unitOfWork;
                _mapper = mapper;
            }

            public async Task<InventoryRouteDto> Handle(Command request, CancellationToken cancellationToken)
            {
                var dto = request.Dto;

                // Get product info
                var product = await _productClient.GetProductByIdAsync(dto.ProductId, cancellationToken)
                    ?? throw new RouteException($"Product {dto.ProductId} not found");

                // Get departments info
                var fromDepartment = await _productClient.GetDepartmentByIdAsync(product.DepartmentId, cancellationToken)
                    ?? throw new RouteException($"Department {product.DepartmentId} not found");

                var toDepartment = await _productClient.GetDepartmentByIdAsync(dto.ToDepartmentId, cancellationToken)
                    ?? throw new RouteException($"Department {dto.ToDepartmentId} not found");

                string? imageUrl = null;

                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                try
                {
                    // Upload image if provided
                    if (dto.ImageFile != null && dto.ImageFile.Length > 0)
                    {
                        using var stream = dto.ImageFile.OpenReadStream();
                        imageUrl = await _imageService.UploadImageAsync(stream, dto.ImageFile.FileName, product.InventoryCode);
                    }

                    // Create product snapshot
                    var productSnapshot = new ProductSnapshot(
                        product.Id,
                        product.InventoryCode,
                        product.Model,
                        product.Vendor,
                        product.CategoryName,
                        product.IsWorking);

                    // Create route
                    var route = InventoryRoute.CreateTransfer(
                        productSnapshot,
                        product.DepartmentId,
                        fromDepartment.Name,
                        dto.ToDepartmentId,
                        toDepartment.Name,
                        dto.FromWorker,
                        dto.ToWorker,
                        imageUrl,
                        dto.Notes);

                    await _repository.AddAsync(route, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    // Update product department
                    await _productClient.UpdateProductInfoAfterRouting(dto.ProductId, dto.ToDepartmentId, cancellationToken);

                    // Complete route
                    route.Complete();
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    // Publish image update event if image was added
                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        await _messagePublisher.PublishAsync(
                            new ProductImageUpdatedEvent
                            {
                                ProductId = dto.ProductId,
                                NewImageUrl = imageUrl,
                                UpdatedAt = DateTime.UtcNow
                            },
                            "product.image.updated",
                            cancellationToken);
                    }

                    await _unitOfWork.CommitTransactionAsync(cancellationToken);

                    return _mapper.Map<InventoryRouteDto>(route);
                }
                catch
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);

                    if (!string.IsNullOrEmpty(imageUrl))
                        await _imageService.DeleteImageAsync(imageUrl);

                    throw;
                }
            }
        }
    }
}
