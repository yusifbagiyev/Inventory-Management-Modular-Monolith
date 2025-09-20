using MediatR;
using Microsoft.Extensions.Logging;
using ProductService.Application.DTOs;
using ProductService.Application.Features.Categories.Queries;
using ProductService.Application.Features.Departments.Queries;
using ProductService.Application.Features.Products.Commands;
using ProductService.Application.Features.Products.Queries;
using ProductService.Application.Interfaces;
using SharedServices.DTOs;
using SharedServices.Enum;
using SharedServices.Exceptions;
using SharedServices.Identity;

namespace ProductService.Application.Services
{
    public class ProductManagementService : IProductManagementService
    {
        private readonly IMediator _mediator;
        private readonly IApprovalService _approvalService;
        private readonly ILogger<ProductManagementService> _logger;

        public ProductManagementService(
            IMediator mediator,
            IApprovalService approvalService,
            ILogger<ProductManagementService> logger)
        {
            _mediator = mediator;
            _approvalService = approvalService;
            _logger = logger;
        }

        public async Task<ProductDto> CreateProductWithApprovalAsync(
            CreateProductDto dto,
            int userId,
            string userName,
            List<string> userPermissions)
        {
            // First, we validate that the product doesn't already exist
            await ValidateProductDoesNotExist(dto.InventoryCode);

            // Check if user has direct permission to bypass approval
            if (userPermissions.Contains(AllPermissions.ProductCreateDirect))
            {
                _logger.LogInformation($"User {userName} creating product {dto.InventoryCode} directly");
                return await _mediator.Send(new CreateProduct.Command(dto));
            }

            // Check if user has permission to create with approval
            if (!userPermissions.Contains(AllPermissions.ProductCreate))
            {
                throw new InsufficientPermissionsException("You don't have permission to create products");
            }

            // Build the approval request with enriched data
            var actionData = await BuildCreateProductActionData(dto);
            var approvalRequest = new CreateApprovalRequestDto
            {
                RequestType = RequestType.CreateProduct,
                EntityType = "Product",
                EntityId = null,
                ActionData = new CreateProductActionData { ProductData = actionData }
            };

            var result = await _approvalService.CreateApprovalRequestAsync(approvalRequest, userId, userName);

            _logger.LogInformation($"Approval request {result.Id} created for product {dto.InventoryCode}");
            throw new ApprovalRequiredException(result.Id, "Product creation request has been submitted for approval");
        }

        public async Task<ProductDto> UpdateProductWithApprovalAsync(
            int id,
            UpdateProductDto dto,
            int userId,
            string userName,
            List<string> userPermissions)
        {
            // First, get the existing product to compare changes
            var existingProduct = await _mediator.Send(new GetProductByIdQuery(id));
            if (existingProduct == null)
            {
                throw new NotFoundException($"Product with ID {id} not found");
            }

            // Check if user has direct update permission
            if (userPermissions.Contains(AllPermissions.ProductUpdateDirect))
            {
                _logger.LogInformation($"User {userName} updating product {id} directly");
                await _mediator.Send(new UpdateProduct.Command(id, dto));
                return await _mediator.Send(new GetProductByIdQuery(id));
            }

            // Check if user has permission to update with approval
            if (!userPermissions.Contains(AllPermissions.ProductUpdate))
            {
                throw new InsufficientPermissionsException("You don't have permission to update products");
            }

            // Build comprehensive change tracking
            var changeComparison = await BuildChangeComparison(existingProduct, dto);

            // Only proceed if there are actual changes
            if (!changeComparison.HasChanges)
            {
                _logger.LogInformation($"No changes detected for product {id}");
                return existingProduct;
            }

            // Create the update data and approval request
            var updateData = await BuildUpdateProductActionData(dto);
            var approvalRequest = new CreateApprovalRequestDto
            {
                RequestType = RequestType.UpdateProduct,
                EntityType = "Product",
                EntityId = id,
                ActionData = new
                {
                    ProductId = id,
                    existingProduct.InventoryCode,
                    UpdateData = updateData,
                    Changes = changeComparison.Changes,
                    ChangesSummary = changeComparison.Summary
                }
            };

            var result = await _approvalService.CreateApprovalRequestAsync(approvalRequest, userId, userName);

            _logger.LogInformation($"Approval request {result.Id} created for updating product {id}");
            throw new ApprovalRequiredException(result.Id, "Product update request submitted for approval");
        }

        public async Task DeleteProductWithApprovalAsync(
            int id,
            int userId,
            string userName,
            List<string> userPermissions)
        {
            // Get product information for the approval request
            var product = await _mediator.Send(new GetProductByIdQuery(id));
            if (product == null)
            {
                throw new NotFoundException($"Product with ID {id} not found");
            }

            // Check if user has direct delete permission
            if (userPermissions.Contains(AllPermissions.ProductDeleteDirect))
            {
                _logger.LogInformation($"User {userName} deleting product {id} directly");
                await _mediator.Send(new DeleteProduct.Command(id, userName));
                return;
            }

            // Check if user has permission to delete with approval
            if (!userPermissions.Contains(AllPermissions.ProductDelete))
            {
                throw new InsufficientPermissionsException("You don't have permission to delete products");
            }

            // Create approval request with product details for audit trail
            var approvalRequest = new CreateApprovalRequestDto
            {
                RequestType = RequestType.DeleteProduct,
                EntityType = "Product",
                EntityId = id,
                ActionData = new
                {
                    ProductId = id,
                    product.InventoryCode,
                    product.Model,
                    product.Vendor,
                    product.DepartmentName,
                    DeleteReason = $"Requested by {userName}"
                }
            };

            var result = await _approvalService.CreateApprovalRequestAsync(approvalRequest, userId, userName);

            _logger.LogInformation($"Approval request {result.Id} created for deleting product {id}");
            throw new ApprovalRequiredException(result.Id, "Product deletion request submitted for approval");
        }

        private async Task ValidateProductDoesNotExist(int inventoryCode)
        {
            var existingProduct = await _mediator.Send(new GetProductByInventoryCodeQuery(inventoryCode));

            // Double-check with a small delay to avoid race conditions
            if (existingProduct != null)
            {
                await Task.Delay(100);
                existingProduct = await _mediator.Send(new GetProductByInventoryCodeQuery(inventoryCode));

                if (existingProduct != null)
                {
                    _logger.LogWarning($"Attempt to create duplicate product with inventory code {inventoryCode}");
                    throw new DuplicateEntityException($"Product with inventory code {inventoryCode} already exists");
                }
            }
        }

        private async Task<Dictionary<string, object>> BuildCreateProductActionData(CreateProductDto dto)
        {
            var actionData = new Dictionary<string, object>
            {
                ["inventoryCode"] = dto.InventoryCode,
                ["model"] = dto.Model ?? "",
                ["vendor"] = dto.Vendor ?? "",
                ["worker"] = dto.Worker ?? "",
                ["description"] = dto.Description ?? "",
                ["isWorking"] = dto.IsWorking,
                ["isActive"] = dto.IsActive,
                ["isNewItem"] = dto.IsNewItem,
                ["categoryId"] = dto.CategoryId,
                ["departmentId"] = dto.DepartmentId
            };

            // Enrich with category and department names for better approval context
            try
            {
                var category = await _mediator.Send(new GetCategoryByIdQuery(dto.CategoryId));
                var department = await _mediator.Send(new GetDepartmentByIdQuery(dto.DepartmentId));

                actionData["categoryName"] = category?.Name ?? $"Category #{dto.CategoryId}";
                actionData["departmentName"] = department?.Name ?? $"Department #{dto.DepartmentId}";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enrich product data with names");
            }

            // Handle image data if present
            if (dto.ImageFile != null && dto.ImageFile.Length > 0)
            {
                using var ms = new MemoryStream();
                await dto.ImageFile.CopyToAsync(ms);
                actionData["imageData"] = Convert.ToBase64String(ms.ToArray());
                actionData["imageFileName"] = dto.ImageFile.FileName;
                actionData["imageSize"] = dto.ImageFile.Length;
            }

            return actionData;
        }

        private async Task<Dictionary<string, object>> BuildUpdateProductActionData(UpdateProductDto dto)
        {
            var updateData = new Dictionary<string, object>
            {
                ["model"] = dto.Model ?? "",
                ["vendor"] = dto.Vendor ?? "",
                ["worker"] = dto.Worker ?? "",
                ["description"] = dto.Description ?? "",
                ["categoryId"] = dto.CategoryId,
                ["departmentId"] = dto.DepartmentId
            };

            // Add image data if present
            if (dto.ImageFile != null && dto.ImageFile.Length > 0)
            {
                using var ms = new MemoryStream();
                await dto.ImageFile.CopyToAsync(ms);
                updateData["imageData"] = Convert.ToBase64String(ms.ToArray());
                updateData["imageFileName"] = dto.ImageFile.FileName;
                updateData["imageSize"] = dto.ImageFile.Length;
            }

            return updateData;
        }

        private async Task<ProductChangeComparison> BuildChangeComparison(ProductDto existing, UpdateProductDto updated)
        {
            var comparison = new ProductChangeComparison();
            var changes = new Dictionary<string, ChangeDetail>();

            // Compare each field and track changes
            if (existing.Model != updated.Model)
            {
                changes["Model"] = new ChangeDetail
                {
                    Old = existing.Model ?? "None",
                    New = updated.Model ?? "None"
                };
            }

            if (existing.Vendor != updated.Vendor)
            {
                changes["Vendor"] = new ChangeDetail
                {
                    Old = existing.Vendor ?? "None",
                    New = updated.Vendor ?? "None"
                };
            }

            if (existing.Worker != updated.Worker)
            {
                changes["Worker"] = new ChangeDetail
                {
                    Old = existing.Worker ?? "None",
                    New = updated.Worker ?? "None"
                };
            }

            if (existing.Description != updated.Description)
            {
                changes["Description"] = new ChangeDetail
                {
                    Old = existing.Description ?? "None",
                    New = updated.Description ?? "None"
                };
            }

            if (existing.CategoryId != updated.CategoryId)
            {
                // Get category names for better context
                var oldCategory = await _mediator.Send(new GetCategoryByIdQuery(existing.CategoryId));
                var newCategory = await _mediator.Send(new GetCategoryByIdQuery(updated.CategoryId));

                changes["Category"] = new ChangeDetail
                {
                    Old = oldCategory?.Name ?? $"Category #{existing.CategoryId}",
                    New = newCategory?.Name ?? $"Category #{updated.CategoryId}"
                };
            }

            if (existing.DepartmentId != updated.DepartmentId)
            {
                // Get department names for better context
                var oldDepartment = await _mediator.Send(new GetDepartmentByIdQuery(existing.DepartmentId));
                var newDepartment = await _mediator.Send(new GetDepartmentByIdQuery(updated.DepartmentId));

                changes["Department"] = new ChangeDetail
                {
                    Old = oldDepartment?.Name ?? $"Department #{existing.DepartmentId}",
                    New = newDepartment?.Name ?? $"Department #{updated.DepartmentId}"
                };
            }

            if (updated.ImageFile != null)
            {
                changes["Image"] = new ChangeDetail
                {
                    Old = "Current Image",
                    New = $"New Image ({updated.ImageFile.FileName})"
                };
            }

            if(existing.IsNewItem!=updated.IsNewItem)
            {
                changes["IsNewItem"] = new ChangeDetail
                {
                    Old = existing.IsNewItem ? "Product is new" : "Product is used",
                    New = updated.IsNewItem ? "Product is new" : "Product is used"
                };
            }

            if (existing.IsActive != updated.IsActive)
            {
                changes["IsActive"] = new ChangeDetail
                {
                    Old = existing.IsActive ? "Product is active" : "Product is not available",
                    New = updated.IsActive ? "Product is active" : "Product is not available"
                };
            }

            if (existing.IsWorking != updated.IsWorking)
            {
                changes["IsWorking"] = new ChangeDetail
                {
                    Old = existing.IsWorking ? "Product is working" : "Product is not working",
                    New = updated.IsWorking ? "Product is working" : "Product is not working"
                };
            }

            comparison.Changes = changes;
            comparison.HasChanges = changes.Any();
            comparison.Summary = GenerateChangeSummary(changes);

            return comparison;
        }

        private string GenerateChangeSummary(Dictionary<string, ChangeDetail> changes)
        {
            if (!changes.Any())
                return "No changes detected";

            var summaryParts = changes.Select(kvp =>
                $"{kvp.Key}: '{kvp.Value.Old}' → '{kvp.Value.New}'"
            );

            return string.Join(", ", summaryParts);
        }
    }
}