using FluentValidation;
using MediatR;
using RouteService.Application.DTOs;
using RouteService.Application.Interfaces;
using RouteService.Domain.Entities;
using RouteService.Domain.Exceptions;
using RouteService.Domain.Repositories;
using RouteService.Domain.ValueObjects;
using SharedServices.Contracts;
using SharedServices.Exceptions;
using SharedServices.Persistence;
using RouteService.Application.Mappings;

namespace RouteService.Application.Features.Routes.Commands
{
    public class TransferInventory
    {
        public record Command(TransferInventoryDto Dto) : IRequest<InventoryRouteDto>, ITransactionalRequest;

        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.Dto.ProductId).GreaterThan(0);
                RuleFor(x => x.Dto.ToDepartmentId).GreaterThan(0);
                RuleFor(x => x.Dto.Notes).MaximumLength(500);
            }
        }

        public class Handler : IRequestHandler<Command, InventoryRouteDto>
        {
            private readonly IInventoryRouteRepository _repository;
            private readonly IProductCatalog _productCatalog;
            private readonly IImageService _imageService;
            private readonly IUnitOfWork _unitOfWork;
            private readonly DbSession _session;

            public Handler(
                IInventoryRouteRepository repository,
                IProductCatalog productCatalog,
                IImageService imageService,
                IUnitOfWork unitOfWork,
                DbSession session)
            {
                _repository = repository;
                _productCatalog = productCatalog;
                _imageService = imageService;
                _unitOfWork = unitOfWork;
                _session = session;
            }

            public async Task<InventoryRouteDto> Handle(Command request, CancellationToken cancellationToken)
            {
                var dto = request.Dto;

                var product = await _productCatalog.GetProductAsync(dto.ProductId, cancellationToken)
                    ?? throw new NotFoundException($"Product {dto.ProductId} not found");

                var toDepartment = await _productCatalog.GetDepartmentAsync(dto.ToDepartmentId, cancellationToken)
                    ?? throw new NotFoundException($"Department {dto.ToDepartmentId} not found");

                // Two open transfers for one product could be completed in either order, leaving the
                // product wherever the last one pointed.
                if (await _repository.HasPendingRouteForProductAsync(product.Id, cancellationToken))
                    throw new RouteException("This product already has a pending transfer. Complete or delete it first.");

                string? imageUrl = null;
                if (dto.ImageFile != null && dto.ImageFile.Length > 0)
                {
                    await using var stream = dto.ImageFile.OpenReadStream();
                    imageUrl = await _imageService.UploadImageAsync(stream, dto.ImageFile.FileName, product.InventoryCode);
                    var uploaded = imageUrl;
                    _session.OnRollback(() => _imageService.DeleteImageAsync(uploaded));
                }

                var route = InventoryRoute.CreateTransfer(
                    new ProductSnapshot(
                        product.Id,
                        product.InventoryCode,
                        product.Model,
                        product.Vendor,
                        product.CategoryName,
                        product.IsWorking),
                    product.DepartmentId,
                    product.DepartmentName,
                    toDepartment.Id,
                    toDepartment.Name,
                    product.Worker,
                    dto.ToWorker,
                    imageUrl,
                    dto.Notes);

                await _repository.AddAsync(route, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return route.ToDto();
            }
        }
    }
}
