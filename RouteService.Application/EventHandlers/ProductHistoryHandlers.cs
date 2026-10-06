using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RouteService.Domain.Entities;
using RouteService.Domain.Repositories;
using RouteService.Domain.ValueObjects;
using SharedServices.Contracts;
using SharedServices.Events;
using SharedServices.Persistence;
using SharedServices.Storage;

namespace RouteService.Application.EventHandlers
{
    /// <summary>Records every product change as a completed route in the product command's transaction.</summary>
    public class ProductHistoryHandlers :
        INotificationHandler<ProductCreatedEvent>,
        INotificationHandler<ProductUpdatedEvent>,
        INotificationHandler<ProductDeletedEvent>
    {
        private readonly IInventoryRouteRepository _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ImageStorage _images;
        private readonly DbSession _session;
        private readonly IWhatsAppRouteNotifier _whatsApp;

        public ProductHistoryHandlers(
            IInventoryRouteRepository repository,
            IUnitOfWork unitOfWork,
            ImageStorage images,
            DbSession session,
            IWhatsAppRouteNotifier whatsApp)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _images = images;
            _session = session;
            _whatsApp = whatsApp;
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
            // The new product's group message goes out after the commit, and this route shows how it went
            if (_whatsApp.Enabled)
                route.SetWhatsAppStatus(WhatsAppStatus.Queued, null);

            await SaveAsync(route, cancellationToken);
        }

        public async Task Handle(ProductUpdatedEvent notification, CancellationToken cancellationToken)
        {
            var before = notification.Before;
            var after = notification.After;

            // A pending transfer shows and completes under the product's current code, details and place
            foreach (var pending in await _repository.GetPendingTransfersForProductAsync(after.ProductId, cancellationToken))
                pending.RefreshSource(Snapshot(after), after.DepartmentId, after.DepartmentName, after.Worker);

            // Only UpdateProductInventoryCode changes the code, and it changes nothing else
            if (before.InventoryCode != after.InventoryCode)
            {
                var codeChange = InventoryRoute.CreateCodeChange(
                    Snapshot(after),
                    after.DepartmentId,
                    after.DepartmentName,
                    after.Worker,
                    notification.Changes);
                codeChange.Complete();
                await SaveAsync(codeChange, cancellationToken);
                return;
            }

            var imageUrl = await CopyImageAsync(notification.NewImageUrl, after.InventoryCode, cancellationToken);

            var route = InventoryRoute.CreateUpdate(
                new ExistingProduct(before.DepartmentId, before.DepartmentName, before.Worker),
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

            // A transfer of a deleted product could never be completed, so it goes with the product
            await _repository.LockProductTransfersAsync(product.ProductId, cancellationToken);
            foreach (var pending in await _repository.GetPendingTransfersForProductAsync(product.ProductId, cancellationToken))
            {
                await _repository.DeleteAsync(pending, cancellationToken);
                foreach (var imageUrl in pending.ImageUrls)
                    _session.AfterCommit((sp, _) => sp.GetRequiredService<ImageStorage>().DeleteAsync(imageUrl));
            }

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

        // History keeps its own copy, so later edits to the product's images can't change it
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
