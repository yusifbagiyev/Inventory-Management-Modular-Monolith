using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Services;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Repositories;
using SharedServices.Contracts;
using SharedServices.Events;
using SharedServices.Identity;
using SharedServices.Storage;

namespace NotificationService.Infrastructure.Services
{
    public class NotificationDispatcher : INotificationDispatcher
    {
        private readonly INotificationRepository _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUserDirectory _users;
        private readonly IHubContext<NotificationHub> _hub;
        private readonly IWhatsAppService _whatsApp;
        private readonly ImageStorage _images;
        private readonly IConfiguration _configuration;
        private readonly ILogger<NotificationDispatcher> _logger;

        public NotificationDispatcher(
            INotificationRepository repository,
            IUnitOfWork unitOfWork,
            IUserDirectory users,
            IHubContext<NotificationHub> hub,
            IWhatsAppService whatsApp,
            ImageStorage images,
            IConfiguration configuration,
            ILogger<NotificationDispatcher> logger)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _users = users;
            _hub = hub;
            _whatsApp = whatsApp;
            _images = images;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task ApprovalRequestCreatedAsync(ApprovalRequestCreatedEvent e, CancellationToken cancellationToken)
        {
            // Everyone who can decide on requests (Admins and approval.decide holders).
            var admins = await _users.GetActiveUserIdsWithPermissionAsync(AllPermissions.ApprovalDecide, cancellationToken);
            var data = Json(new { approvalRequestId = e.RequestId, requestType = e.RequestType, requestedBy = e.RequestedByName });

            await SaveAndPushAsync(admins.Select(adminId => new Notification(
                adminId,
                "ApprovalRequest",
                $"New {ReadableRequestType(e.RequestType)} Request",
                $"{e.RequestedByName} has requested to {ActionDescription(e.RequestType)}. Request #{e.RequestId} needs your approval.",
                data)), cancellationToken);

            await _hub.Clients.Groups(admins.Select(id => $"user-{id}").ToList())
                .SendAsync("RefreshApprovals", new { requestId = e.RequestId, requestType = e.RequestType }, cancellationToken);
        }

        public Task ApprovalRequestProcessedAsync(ApprovalRequestProcessedEvent e, CancellationToken cancellationToken)
        {
            var action = ActionDescription(e.RequestType);
            var (title, message) = e.Status switch
            {
                "Approved" => ("Request Approved ✓",
                    $"Your request to {action} (Request #{e.RequestId}) has been approved by {e.ProcessedByName}."),
                "Rejected" => ("Request Rejected ✗",
                    $"Your request to {action} (Request #{e.RequestId}) has been rejected by {e.ProcessedByName}."
                    + (string.IsNullOrEmpty(e.Reason) ? "" : $" Reason: {e.Reason}")),
                "Failed" => ("Request Failed ⚠",
                    $"Your request to {action} (Request #{e.RequestId}) was approved but failed to execute."
                    + (string.IsNullOrEmpty(e.Reason) ? "" : $" Error: {e.Reason}")),
                _ => ("Request Updated",
                    $"Your {ReadableRequestType(e.RequestType)} request (#{e.RequestId}) status has been updated to: {e.Status}")
            };

            return SaveAndPushAsync(
                [new Notification(
                    e.RequestedById,
                    "ApprovalResponse",
                    title,
                    message,
                    Json(new { approvalRequestId = e.RequestId, status = e.Status, processedBy = e.ProcessedByName }))],
                cancellationToken);
        }

        public async Task ApprovalRequestCancelledAsync(ApprovalRequestCancelledEvent e, CancellationToken cancellationToken)
        {
            var deleted = await _repository.DeleteByApprovalRequestAsync(e.RequestId, cancellationToken);
            _logger.LogInformation("Deleted {Count} notification(s) for cancelled request {RequestId}", deleted, e.RequestId);
        }

        public async Task ProductCreatedAsync(ProductCreatedEvent e, int? actorId, CancellationToken cancellationToken)
        {
            var product = e.Product;
            var users = await OtherActiveUsersAsync(actorId, AllPermissions.ProductView, cancellationToken);
            var data = Json(new { productId = product.ProductId, inventoryCode = product.InventoryCode, model = product.Model });

            await SaveAndPushAsync(users.Select(userId => new Notification(
                userId,
                "ProductUpdate",
                "New Product Added",
                $"Product {product.Model} by {product.Vendor} (Code: {product.InventoryCode}) has been added to {product.DepartmentName}",
                data)), cancellationToken);

            await SendWhatsAppAsync(new WhatsAppProductNotification
            {
                ProductId = product.ProductId,
                InventoryCode = product.InventoryCode,
                Model = product.Model,
                Vendor = product.Vendor,
                CategoryName = product.CategoryName,
                ToDepartmentName = product.DepartmentName,
                ToWorker = product.Worker,
                CreatedAt = e.CreatedAt,
                IsNewItem = product.IsNewItem,
                IsWorking = product.IsWorking,
                Notes = product.Description,
                NotificationType = "created",
                ImageUrl = product.ImageUrl
            }, $"product_{product.InventoryCode}.jpg", cancellationToken);
        }

        public async Task ProductDeletedAsync(ProductDeletedEvent e, int? actorId, CancellationToken cancellationToken)
        {
            var product = e.Product;
            var users = await OtherActiveUsersAsync(actorId, AllPermissions.ProductView, cancellationToken);
            var data = Json(new { productId = product.ProductId, inventoryCode = product.InventoryCode, departmentName = product.DepartmentName });

            await SaveAndPushAsync(users.Select(userId => new Notification(
                userId,
                "ProductUpdate",
                "Product Deleted",
                $"Product {product.Model} (Code: {product.InventoryCode}) has been deleted from {product.DepartmentName}",
                data)), cancellationToken);
        }

        public async Task RouteCompletedAsync(RouteCompletedEvent e, int? actorId, CancellationToken cancellationToken)
        {
            var users = await OtherActiveUsersAsync(actorId, AllPermissions.RouteView, cancellationToken);
            var data = Json(new { routeId = e.RouteId, productId = e.ProductId });

            await SaveAndPushAsync(users.Select(userId => new Notification(
                userId,
                "RouteUpdate",
                "Transfer Completed",
                $"Product {e.Model} (Code: {e.InventoryCode}) transfer to {e.ToDepartmentName} has been completed",
                data)), cancellationToken);

            await SendWhatsAppAsync(new WhatsAppProductNotification
            {
                ProductId = e.ProductId,
                InventoryCode = e.InventoryCode,
                Model = e.Model,
                Vendor = e.Vendor,
                CategoryName = e.CategoryName,
                FromDepartmentName = e.FromDepartmentName,
                FromWorker = e.FromWorker,
                ToDepartmentName = e.ToDepartmentName,
                ToWorker = e.ToWorker,
                CreatedAt = e.CompletedAt,
                Notes = e.Notes,
                NotificationType = "transferred",
                ImageUrl = e.ImageUrl
            }, $"route_{e.InventoryCode}.jpg", cancellationToken);
        }

        /// <summary>Active users who may see the record (<paramref name="permission"/>), except the actor.</summary>
        private async Task<IEnumerable<int>> OtherActiveUsersAsync(int? actorId, string permission, CancellationToken cancellationToken)
            => (await _users.GetActiveUserIdsWithPermissionAsync(permission, cancellationToken)).Where(id => id != actorId);

        /// <summary>Persists the whole fan-out in one SaveChanges, then pushes each over SignalR.</summary>
        private async Task SaveAndPushAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken)
        {
            var list = notifications.ToList();
            if (list.Count == 0) return;

            await _repository.AddRangeAsync(list, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            foreach (var n in list)
            {
                await _hub.Clients.Group($"user-{n.UserId}").SendAsync("ReceiveNotification", new
                {
                    id = n.Id,
                    type = n.Type,
                    title = n.Title,
                    message = n.Message,
                    createdAt = n.CreatedAt,
                    isRead = n.IsRead,
                    data = n.Data
                }, cancellationToken);
            }
        }

        private async Task SendWhatsAppAsync(WhatsAppProductNotification notification, string fallbackFileName, CancellationToken cancellationToken)
        {
            if (!_configuration.GetValue("WhatsApp:Enabled", true))
                return;

            var groupId = _configuration["WhatsApp:DefaultGroupId"];
            if (string.IsNullOrEmpty(groupId))
            {
                _logger.LogWarning("WhatsApp:DefaultGroupId is not configured; skipping WhatsApp notification");
                return;
            }

            try
            {
                var message = _whatsApp.FormatNotification(notification);
                var image = await _images.ReadAsync(notification.ImageUrl, cancellationToken);

                var sent = image != null
                    ? await _whatsApp.SendGroupMessageWithImageDataAsync(
                        groupId, message, image, Path.GetFileName(notification.ImageUrl) ?? fallbackFileName)
                    : await _whatsApp.SendGroupMessageAsync(groupId, message);

                if (!sent)
                    _logger.LogWarning("WhatsApp notification for inventory code {Code} was not delivered", notification.InventoryCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WhatsApp notification for inventory code {Code} failed", notification.InventoryCode);
            }
        }

        private static string Json(object value) => JsonSerializer.Serialize(value);

        private static string ReadableRequestType(string requestType) => requestType switch
        {
            "product.create" => "Product Creation",
            "product.update" => "Product Update",
            "product.delete" => "Product Deletion",
            "product.transfer" => "Product Transfer",
            "route.update" => "Route Update",
            "route.delete" => "Route Deletion",
            _ => requestType.Replace(".", " ")
        };

        private static string ActionDescription(string requestType) => requestType switch
        {
            "product.create" => "create a new product",
            "product.update" => "update product information",
            "product.delete" => "delete a product",
            "product.transfer" => "transfer a product to another department",
            "route.update" => "update route information",
            "route.delete" => "delete a route",
            _ => requestType.Replace(".", " ")
        };
    }
}
