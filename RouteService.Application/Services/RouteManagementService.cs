using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using RouteService.Application.DTOs;
using RouteService.Application.Features.Routes.Commands;
using RouteService.Application.Features.Routes.Queries;
using RouteService.Application.Interfaces;
using RouteService.Domain.Entities;
using RouteService.Domain.Exceptions;
using SharedServices.Contracts;
using SharedServices.Storage;
using SharedServices.DTOs;
using SharedServices.Enum;
using SharedServices.Exceptions;
using SharedServices.Identity;
using System.Security.Claims;

namespace RouteService.Application.Services
{
    /// <summary>Runs a route write directly with its .direct permission, otherwise submits it for approval.</summary>
    public class RouteManagementService : IRouteManagementService
    {
        private readonly IMediator _mediator;
        private readonly IApprovalRequests _approvalRequests;
        private readonly IProductCatalog _productCatalog;
        private readonly IValidator<TransferInventory.Command> _transferValidator;
        private readonly IValidator<UpdateRoute.Command> _updateValidator;
        private readonly ILogger<RouteManagementService> _logger;

        public RouteManagementService(
            IMediator mediator,
            IApprovalRequests approvalRequests,
            IProductCatalog productCatalog,
            IValidator<TransferInventory.Command> transferValidator,
            IValidator<UpdateRoute.Command> updateValidator,
            ILogger<RouteManagementService> logger)
        {
            _mediator = mediator;
            _approvalRequests = approvalRequests;
            _productCatalog = productCatalog;
            _transferValidator = transferValidator;
            _updateValidator = updateValidator;
            _logger = logger;
        }

        public async Task<InventoryRouteDto> TransferInventoryWithApprovalAsync(
            TransferInventoryDto dto,
            int userId,
            string userName,
            List<string> userPermissions)
        {
            if (userPermissions.Contains(AllPermissions.RouteCreateDirect))
            {
                _logger.LogInformation($"User {userName} creating transfer for product {dto.ProductId} directly");
                return await _mediator.Send(new TransferInventory.Command(dto));
            }

            if (!userPermissions.Contains(AllPermissions.RouteCreate))
            {
                throw new InsufficientPermissionsException("You don't have permission to create transfers");
            }

            // What the command would refuse on approval is refused before it reaches the queue
            await _transferValidator.ValidateAndThrowAsync(new TransferInventory.Command(dto));

            var transferData = await BuildTransferApprovalData(dto);

            var approvalRequest = new CreateApprovalRequestDto
            {
                RequestType = RequestType.TransferProduct,
                EntityType = "Route",
                EntityId = null,
                ActionData = transferData
            };

            var requestId = await _approvalRequests.SubmitAsync(approvalRequest, userId, userName);

            _logger.LogInformation($"Approval request {requestId} created for transfer of product {dto.ProductId}");
            throw new ApprovalRequiredException(requestId, "Transfer request has been submitted for approval");
        }



        public async Task UpdateRouteWithApprovalAsync(
            int id,
            UpdateRouteDto dto,
            int userId,
            string userName,
            List<string> userPermissions)
        {
            var existingRoute = await _mediator.Send(new GetRouteByIdQuery(id));
            if (existingRoute == null)
            {
                throw new NotFoundException($"Route with ID {id} not found");
            }

            if (existingRoute.IsCompleted)
            {
                throw new InvalidOperationException("Cannot update a completed route");
            }

            if (userPermissions.Contains(AllPermissions.RouteUpdateDirect))
            {
                _logger.LogInformation($"User {userName} updating route {id} directly");
                await _mediator.Send(new UpdateRoute.Command(id, dto));
                return;
            }

            if (!userPermissions.Contains(AllPermissions.RouteUpdate))
            {
                throw new InsufficientPermissionsException("You don't have permission to update routes");
            }

            await _updateValidator.ValidateAndThrowAsync(new UpdateRoute.Command(id, dto));

            var (updateData, changes) = await BuildRouteUpdateData(existingRoute, dto);
            if (changes.Count == 0)
            {
                throw new RouteException("No changes were made to the route.");
            }

            var approvalRequest = new CreateApprovalRequestDto
            {
                RequestType = RequestType.UpdateRoute,
                EntityType = "Route",
                EntityId = id,
                ActionData = new
                {
                    RouteId = id,
                    UpdateData = updateData,
                    existingRoute.InventoryCode,
                    existingRoute.Model,
                    existingRoute.FromDepartmentName,
                    existingRoute.ToDepartmentName,
                    CurrentNotes = existingRoute.Notes,
                    Changes = changes
                }
            };

            var requestId = await _approvalRequests.SubmitAsync(approvalRequest, userId, userName);

            _logger.LogInformation($"Approval request {requestId} created for updating route {id}");
            throw new ApprovalRequiredException(requestId, "Route update request submitted for approval");
        }



        public async Task DeleteRouteWithApprovalAsync(
            int id,
            int userId,
            string userName,
            List<string> userPermissions)
        {
            var route = await _mediator.Send(new GetRouteByIdQuery(id));
            if (route == null)
            {
                throw new NotFoundException($"Route with ID {id} not found");
            }

            if (route.IsCompleted)
            {
                throw new InvalidOperationException("Cannot delete completed routes. They are part of the audit trail.");
            }

            if (userPermissions.Contains(AllPermissions.RouteDeleteDirect))
            {
                _logger.LogInformation($"User {userName} deleting route {id} directly");
                await _mediator.Send(new DeleteRoute.Command(id));
                return;
            }

            if (!userPermissions.Contains(AllPermissions.RouteDelete))
            {
                throw new InsufficientPermissionsException("You don't have permission to delete routes");
            }

            var approvalRequest = new CreateApprovalRequestDto
            {
                RequestType = RequestType.DeleteRoute,
                EntityType = "Route",
                EntityId = id,
                ActionData = new DeleteRouteActionDataWithRequest
                {
                    RouteId = id,
                    RouteType = route.RouteTypeName,
                    ProductInfo = $"{route.InventoryCode} - {route.Model} ({route.Vendor})",
                    FromLocation = $"{route.FromDepartmentName} - {route.FromWorker}",
                    ToLocation = $"{route.ToDepartmentName} - {route.ToWorker}",
                    CreatedDate = route.CreatedAt,
                    IsCompleted = route.IsCompleted
                }
            };

            var requestId = await _approvalRequests.SubmitAsync(approvalRequest, userId, userName);

            _logger.LogInformation($"Approval request {requestId} created for deleting route {id}");
            throw new ApprovalRequiredException(requestId, "Route deletion request submitted for approval");
        }


        private async Task<Dictionary<string, object>> BuildTransferApprovalData(TransferInventoryDto dto)
        {
            var product = await _productCatalog.GetProductAsync(dto.ProductId);
            if (product == null)
            {
                throw new NotFoundException($"Product {dto.ProductId} not found");
            }

            var toDepartment = await _productCatalog.GetDepartmentAsync(dto.ToDepartmentId);
            if (toDepartment == null)
            {
                throw new NotFoundException($"Target department {dto.ToDepartmentId} not found");
            }
            // Refuse it before it reaches the approval queue
            if (!toDepartment.IsActive)
            {
                throw new RouteException($"The department {toDepartment.Name} is inactive. Choose an active department.");
            }
            InventoryRoute.RequireMove(product.DepartmentId, product.Worker, toDepartment.Id, dto.ToWorker);

            var actionData = new Dictionary<string, object>
            {
                ["productId"] = dto.ProductId,
                ["inventoryCode"] = product.InventoryCode,
                ["productModel"] = product.Model ?? "",
                ["productVendor"] = product.Vendor ?? "",
                ["productCategory"] = product.CategoryName ?? "",
                ["fromDepartmentId"] = product.DepartmentId,
                ["fromDepartmentName"] = product.DepartmentName,
                ["fromWorker"] = product.Worker ?? "",
                ["toDepartmentId"] = dto.ToDepartmentId,
                ["toDepartmentName"] = toDepartment.Name,
                ["toWorker"] = dto.ToWorker ?? "",
                ["notes"] = dto.Notes ?? "",
                ["transferReason"] = BuildTransferReason(product, toDepartment)
            };

            var images = ImageSet.Files(dto.ImageFile, dto.ImageFiles);
            if (images.Count > 0)
                actionData["images"] = await ApprovalActionData.EncodeImagesAsync(images);

            return actionData;
        }


        /// <summary>The request payload with only the fields that change, and one line per change for the approver.</summary>
        private async Task<(Dictionary<string, object> Data, List<string> Changes)> BuildRouteUpdateData(
            InventoryRouteDto existing, UpdateRouteDto updated)
        {
            var updateData = new Dictionary<string, object>();
            var changes = new List<string>();

            // A field goes into the payload only when it changes, so approval leaves the others as they are by then
            if (TextChanges(updated.Notes, existing.Notes))
            {
                updateData["notes"] = updated.Notes!.Trim();
                changes.Add(string.IsNullOrWhiteSpace(updated.Notes) ? "Notes cleared" : "Notes updated");
            }

            if (TextChanges(updated.ToWorker, existing.ToWorker))
            {
                updateData["toWorker"] = updated.ToWorker!.Trim();
                changes.Add($"Worker: {OrNone(existing.ToWorker)} -> {OrNone(updated.ToWorker)}");
            }

            if (updated.ToDepartmentId.HasValue && updated.ToDepartmentId.Value != existing.ToDepartmentId)
            {
                var department = await _productCatalog.GetDepartmentAsync(updated.ToDepartmentId.Value)
                    ?? throw new NotFoundException($"Department with ID {updated.ToDepartmentId.Value} not found");
                if (!department.IsActive)
                {
                    throw new RouteException($"The department {department.Name} is inactive. Choose an active department.");
                }

                updateData["toDepartmentId"] = department.Id;
                changes.Add($"Destination: {existing.ToDepartmentName} -> {department.Name}");
            }

            // Refused here like on a direct edit, when the changed destination or worker would move nothing
            if (updateData.ContainsKey("toDepartmentId") || updateData.ContainsKey("toWorker"))
            {
                var product = await _productCatalog.GetProductAsync(existing.ProductId);
                if (product != null)
                {
                    InventoryRoute.RequireMove(
                        product.DepartmentId,
                        product.Worker,
                        updated.ToDepartmentId ?? existing.ToDepartmentId,
                        updateData.ContainsKey("toWorker") ? updated.ToWorker : existing.ToWorker);
                }
            }

            // A single legacy ImageFile still means replace all images
            if (updated.ImageFile is { Length: > 0 })
                updateData["replaceImages"] = await ApprovalActionData.EncodeImagesAsync([updated.ImageFile]);
            if (updated.ImageFiles?.Any(f => f.Length > 0) == true)
                updateData["images"] = await ApprovalActionData.EncodeImagesAsync(updated.ImageFiles);
            if (updated.RemoveImageUrls?.Count > 0)
                updateData["removeImageUrls"] = updated.RemoveImageUrls;
            if (!string.IsNullOrEmpty(updated.CoverImageUrl))
                updateData["coverImageUrl"] = updated.CoverImageUrl;
            if (ImageSet.Changes(existing.ImageUrls, updated.RemoveImageUrls,
                    ImageSet.Files(updated.ImageFile, updated.ImageFiles).Count, updated.CoverImageUrl))
                changes.Add("Images updated");

            updateData["changesSummary"] = string.Join(", ", changes);

            return (updateData, changes);
        }

        /// <summary>True when the field was sent and differs from the stored text, with blank and missing as the same.</summary>
        private static bool TextChanges(string? sent, string? stored)
            => sent != null && !string.Equals(sent.Trim(), (stored ?? "").Trim(), StringComparison.Ordinal);

        private static string OrNone(string? value) => string.IsNullOrWhiteSpace(value) ? "None" : value.Trim();



        private static string BuildTransferReason(ProductSummary product, DepartmentSummary to)
        {
            return $"Transfer of {product.Model} ({product.InventoryCode}) from {product.DepartmentName} to {to.Name}";
        }


        public int GetUserId(ClaimsPrincipal User)
        {
            // API-key clients carry a service id rather than a user id, so they get 0 and the permission check decides
            var raw = User.FindFirst("UserId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(raw, out var id) ? id : 0;
        }



        public string GetUserName(ClaimsPrincipal User)
        {
            return User.Identity?.Name ?? "Unknown";
        }



        public List<string> GetUserPermissions(ClaimsPrincipal User)
        {
            return User.Claims
                .Where(c => c.Type == "permission")
                .Select(c => c.Value)
                .ToList();
        }
    }
}