using AutoMapper;
using FluentValidation;
using MediatR;
using RouteService.Application.DTOs;
using RouteService.Application.DTOs.Commands;
using RouteService.Application.Interfaces;
using RouteService.Domain.Entities;
using RouteService.Domain.Exceptions;
using RouteService.Domain.Repositories;
using RouteService.Domain.ValueObjects;

namespace RouteService.Application.Features.Routes.Commands
{
    public class RemoveInventory
    {
        public record Command(RemoveInventoryDto Dto) : IRequest<InventoryRouteDto>;

        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.Dto.ProductId).GreaterThan(0);
                RuleFor(x => x.Dto.FromWorker).NotEmpty().MaximumLength(100);
                RuleFor(x => x.Dto.Reason).NotEmpty().MaximumLength(500);
            }
        }

        public class Handler : IRequestHandler<Command, InventoryRouteDto>
        {
            private readonly IInventoryRouteRepository _repository;
            private readonly IProductServiceClient _productClient;
            private readonly IImageService _imageService;
            private readonly IUnitOfWork _unitOfWork;
            private readonly IMapper _mapper;

            public Handler(
                IInventoryRouteRepository repository,
                IProductServiceClient productClient,
                IImageService imageService,
                IUnitOfWork unitOfWork,
                IMapper mapper)
            {
                _repository = repository;
                _productClient = productClient;
                _imageService = imageService;
                _unitOfWork = unitOfWork;
                _mapper = mapper;
            }

            public async Task<InventoryRouteDto> Handle(Command request, CancellationToken cancellationToken)
            {
                var dto = request.Dto;

                // Get product info
                var product = await _productClient.GetProductByIdAsync(dto.ProductId, cancellationToken)
                    ?? throw new RouteException($"Product {dto.ProductId} not found");

                // Get department info
                var department = await _productClient.GetDepartmentByIdAsync(product.DepartmentId, cancellationToken)
                    ?? throw new RouteException($"Department {product.DepartmentId} not found");

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
                    var route = InventoryRoute.CreateRemoval(
                        productSnapshot,
                        product.DepartmentId,
                        department.Name,
                        dto.FromWorker,
                        dto.Reason,
                        imageUrl);

                    await _repository.AddAsync(route, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    // Update product status to inactive
                    await _productClient.UpdateProductStatusAsync(dto.ProductId, false, cancellationToken);

                    // Complete route
                    route.Complete();
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    // After creating the route, update product image in ProductService
                    if (dto.ImageFile != null && dto.ImageFile.Length > 0)
                    {
                        using var stream = dto.ImageFile.OpenReadStream();
                        await _productClient.UpdateProductImageAsync(
                            dto.ProductId,
                            stream,
                            dto.ImageFile.FileName,
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
