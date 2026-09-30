using MediatR;
using Microsoft.Extensions.Logging;
using RouteService.Application.DTOs;
using RouteService.Application.Features.Routes.Commands;
using RouteService.Application.Features.Routes.Queries;
using RouteService.Application.Interfaces;
using SharedServices.Contracts;
using SharedServices.Storage;
using SharedServices.DTOs;
using SharedServices.Enum;
using SharedServices.Exceptions;
using SharedServices.Identity;
using System.Security.Claims;

namespace RouteService.Application.Services
{
    public class RouteManagementService : IRouteManagementService
    {
        private readonly IMediator _mediator;
        private readonly IApprovalRequests _approvalRequests;
        private readonly IProductCatalog _productCatalog;
        private readonly ILogger<RouteManagementService> _logger;

        public RouteManagementService(
            IMediator mediator,
            IApprovalRequests approvalRequests,
            IProductCatalog productCatalog,
            ILogger<RouteManagementService> logger)
        {
            _mediator = mediator;
            _approvalRequests = approvalRequests;
            _productCatalog = productCatalog;
            _logger = logger;
        }

        public async Task<InventoryRouteDto> TransferInventoryWithApprovalAsync(
            TransferInventoryDto dto,
            int userId,
            string userName,
            List<string> userPermissions)
        {
            // Check if user has direct permission
            if (userPermissions.Contains(AllPermissions.RouteCreateDirect))
            {
                _logger.LogInformation($"User {userName} creating transfer for product {dto.ProductId} directly");
                return await _mediator.Send(new TransferInventory.Command(dto));
            }

            // Check if user has permission to create with approval
            if (!userPermissions.Contains(AllPermissions.RouteCreate))
            {
                throw new InsufficientPermissionsException("You don't have permission to create transfers");
            }

            // Build comprehensive transfer data for approval
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
            // Get existing route for comparison
            var existingRoute = await _mediator.Send(new GetRouteByIdQuery(id));
            if (existingRoute == null)
            {
                throw new NotFoundException($"Route with ID {id} not found");
            }

            // Check if route is already completed
            if (existingRoute.IsCompleted)
            {
                throw new InvalidOperationException("Cannot update a completed route");
            }

            // Check if user has direct permission
            if (userPermissions.Contains(AllPermissions.RouteUpdateDirect))
            {
                _logger.LogInformation($"User {userName} updating route {id} directly");
                await _mediator.Send(new UpdateRoute.Command(id, dto));
                return;
            }

            // Check if user has permission to update with approval
            if (!userPermissions.Contains(AllPermissions.RouteUpdate))
            {
                throw new InsufficientPermissionsException("You don't have permission to update routes");
            }

            // Build update data with change tracking
            var updateData = await BuildRouteUpdateData(existingRoute, dto);

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
                    Changes = BuildRouteChangeSummary(existingRoute, dto)
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
            // Get route information for the approval request
            var route = await _mediator.Send(new GetRouteByIdQuery(id));
            if (route == null)
            {
                throw new NotFoundException($"Route with ID {id} not found");
            }

            // Business rule: Cannot delete completed routes
            if (route.IsCompleted)
            {
                throw new InvalidOperationException("Cannot delete completed routes. They are part of the audit trail.");
            }

            // Check if user has direct permission
            if (userPermissions.Contains(AllPermissions.RouteDeleteDirect))
            {
                _logger.LogInformation($"User {userName} deleting route {id} directly");
                await _mediator.Send(new DeleteRoute.Command(id));
                return;
            }

            // Check if user has permission to delete with approval
            if (!userPermissions.Contains(AllPermissions.RouteDelete))
            {
                throw new InsufficientPermissionsException("You don't have permission to delete routes");
            }

            // Create approval request with route details
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
            // Fetch comprehensive product information
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
            // Refused before it reaches the approval queue, too.
            if (!toDepartment.IsActive)
            {
                throw new RouteService.Domain.Exceptions.RouteException($"The department {toDepartment.Name} is inactive. Choose an active department.");
            }

            
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


        private async Task<Dictionary<string, object>> BuildRouteUpdateData(InventoryRouteDto existing, UpdateRouteDto updated)
        {
            var updateData = new Dictionary<string, object>
            {
                ["notes"] = updated.Notes ?? existing.Notes ?? ""
            };

            // Track what's changing
            var changes = new List<string>();

            if (existing.Notes != updated.Notes && !string.IsNullOrEmpty(updated.Notes))
            {
                changes.Add($"Notes updated");
            }

            // Worker and destination were never carried into the approval payload, so an approved
            // request silently applied neither. Include them whenever they actually change.
            if (updated.ToWorker != null && updated.ToWorker != existing.ToWorker)
            {
                updateData["toWorker"] = updated.ToWorker;
                changes.Add(string.IsNullOrWhiteSpace(updated.ToWorker)
                    ? "Worker cleared"
                    : $"Worker changed to {updated.ToWorker}");
            }

            if (updated.ToDepartmentId.HasValue && updated.ToDepartmentId.Value != existing.ToDepartmentId)
            {
                updateData["toDepartmentId"] = updated.ToDepartmentId.Value;
                changes.Add("Destination department changed");
            }

            // Images. A single legacy ImageFile keeps its "replace the images" meaning.
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

            return updateData;
        }


        private string BuildRouteChangeSummary(InventoryRouteDto existing, UpdateRouteDto updated)
        {
            var changes = new List<string>();

            if (!string.IsNullOrEmpty(updated.Notes) && existing.Notes != updated.Notes)
            {
                changes.Add("Notes updated");
            }

            if (updated.ToWorker != null && updated.ToWorker != existing.ToWorker)
            {
                changes.Add(string.IsNullOrWhiteSpace(updated.ToWorker)
                    ? "Worker cleared"
                    : $"Worker: {existing.ToWorker} -> {updated.ToWorker}");
            }

            if (updated.ToDepartmentId.HasValue && updated.ToDepartmentId.Value != existing.ToDepartmentId)
            {
                changes.Add($"Destination: {existing.ToDepartmentName} -> department #{updated.ToDepartmentId.Value}");
            }

            if (ImageSet.Changes(existing.ImageUrls, updated.RemoveImageUrls,
                    ImageSet.Files(updated.ImageFile, updated.ImageFiles).Count, updated.CoverImageUrl))
            {
                changes.Add("Images updated");
            }

            return changes.Any() ? string.Join(", ", changes) : "No changes";
        }



        private static string BuildTransferReason(ProductSummary product, DepartmentSummary to)
        {
            return $"Transfer of {product.Model} ({product.InventoryCode}) from {product.DepartmentName} to {to.Name}";
        }


        public int GetUserId(ClaimsPrincipal User)
        {
            // API-key clients carry a service id, not a user id: 0, and the permission check answers.
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