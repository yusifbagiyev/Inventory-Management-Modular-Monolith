using MediatR;
using RouteService.Domain.Entities;
using RouteService.Domain.Repositories;
using RouteService.Domain.ValueObjects;
using SharedServices.Events;
using SharedServices.Persistence;
using SharedServices.Storage;

namespace RouteService.Application.EventHandlers
{
    /// <summary>
    /// Records every product change as a completed route, forming the product's audit trail.
    /// Runs inside the product command's transaction, so a product change and its history row
    /// are committed together. Images are copied so later product edits cannot alter history.
    /// </summary>
    public class ProductHistoryHandlers :
        INotificationHandler<ProductCreatedEvent>,
        INotificationHandler<ProductUpdatedEvent>,
        INotificationHandler<ProductDeletedEvent>
    {
        private readonly IInventoryRouteRepository _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ImageStorage _images;
        private readonly DbSession _session;

        public ProductHistoryHandlers(
            IInventoryRouteRepository repository,
            IUnitOfWork unitOfWork,
            ImageStorage images,
            DbSession session)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _images = images;
            _session = session;
        }

        public async Task Handle(ProductCreatedEvent notification, CancellationToken cancellationToken)
        {
            var product = notification.Product;
            var imageUrl = await CopyImageAsync(product.ImageUrl, product.InventoryCode, cancellationToken);

            var route = InventoryRoute.CreateNewInventory(
                Snapshot(product),
                product.DepartmentId,
                product.DepartmentName,
                product.Worker,
                product.IsNewItem,
                imageUrl,
                "Auto-created from product service");
            route.Complete();

            await SaveAsync(route, cancellationToken);
        }

        public async Task Handle(ProductUpdatedEvent notification, CancellationToken cancellationToken)
        {
            var before = notification.Before;
            var after = notification.After;
            var imageUrl = await CopyImageAsync(notification.NewImageUrl, after.InventoryCode, cancellationToken);

            var route = InventoryRoute.CreateUpdate(
                new ExistingProduct(
                    before.ProductId,
                    before.InventoryCode,
                    before.CategoryId,
                    before.CategoryName,
                    before.DepartmentId,
                    before.DepartmentName,
                    before.Worker,
                    before.Description,
                    before.IsActive,
                    before.IsNewItem,
                    before.IsWorking),
                Snapshot(after),
                after.DepartmentId,
                after.DepartmentName,
                after.Worker,
                imageUrl,
                $"Product updated: {notification.Changes}");
            route.Complete();

            await SaveAsync(route, cancellationToken);
        }

        public async Task Handle(ProductDeletedEvent notification, CancellationToken cancellationToken)
        {
            var product = notification.Product;

            var route = InventoryRoute.CreateRemoval(
                Snapshot(product),
                product.DepartmentId,
                product.DepartmentName,
                product.Worker ?? "No Worker",
                notification.RemovedBy,
                $"Product removed by {notification.RemovedBy}");
            route.Complete();

            await SaveAsync(route, cancellationToken);
        }

        private static ProductSnapshot Snapshot(ProductState product) => new(
            product.ProductId,
            product.InventoryCode,
            product.Model,
            product.Vendor,
            product.CategoryName,
            product.IsWorking);

        private async Task<string?> CopyImageAsync(string? productImageUrl, int inventoryCode, CancellationToken cancellationToken)
        {
            var copy = await _images.CopyAsync(productImageUrl, ImageStorage.Routes, inventoryCode, cancellationToken);
            if (copy != null)
                _session.OnRollback(() => _images.DeleteAsync(copy));
            return copy;
        }

        private async Task SaveAsync(InventoryRoute route, CancellationToken cancellationToken)
        {
            await _repository.AddAsync(route, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
