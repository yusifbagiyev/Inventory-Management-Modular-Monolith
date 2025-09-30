using MediatR;
using Microsoft.Extensions.Logging;
using ProductService.Application.DTOs;
using ProductService.Application.Features.Categories.Queries;
using ProductService.Application.Features.Departments.Queries;
using ProductService.Application.Features.Products.Commands;
using ProductService.Application.Features.Products.Queries;
using ProductService.Application.Interfaces;
using ProductService.Domain.Repositories;
using SharedServices.DTOs;
using SharedServices.Enum;
using SharedServices.Exceptions;
using SharedServices.Identity;
using System.Security.Claims;

namespace ProductService.Application.Services
{
    public class ProductManagementService : IProductManagementService
    {
        private readonly IMediator _mediator;
        private readonly IApprovalService _approvalService;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IDepartmentRepository _departmentRepository;
        private readonly ILogger<ProductManagementService> _logger;

        public ProductManagementService(
            IMediator mediator,
            IApprovalService approvalService,
            ICategoryRepository categoryRepository,
            IDepartmentRepository departmentRepository,
            ILogger<ProductManagementService> logger)
        {
            _mediator = mediator;
            _approvalService = approvalService;
            _categoryRepository = categoryRepository;
            _departmentRepository = departmentRepository;
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
                ActionData = new 
                {
                    ProductData = actionData
                }
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
            var existingProduct = await GetProductById(id);

            // Build comprehensive change tracking
            var changeComparison = await TrackWhatChanges(existingProduct, dto);

            // Only proceed if there are actual changes
            if (!changeComparison.Any())
            {
                _logger.LogInformation($"No changes detected for product {id}");
                return existingProduct;
            }

            // Check if user has direct update permission
            if (userPermissions.Contains(AllPermissions.ProductUpdateDirect))
            {
                _logger.LogInformation($"User {userName} updating product {id} directly");
                await _mediator.Send(new UpdateProduct.Command(id, dto,changeComparison));
                return await GetProductById(id);
            }


            // Check if user has permission to update with approval
            if (!userPermissions.Contains(AllPermissions.ProductUpdate))
            {
                throw new InsufficientPermissionsException("You don't have permission to update products");
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
                    Changes = changeComparison
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
            var product = await GetProductById(id);

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



        private async Task<ProductDto> GetProductById(int id)
        {
            return await _mediator.Send(new GetProductByIdQuery(id)) ??
                throw new NotFoundException($"Product with ID {id} not found");
        }



        private async Task ValidateProductDoesNotExist(int inventoryCode)
        {
            var existingProduct = await _mediator.Send(new GetProductByInventoryCodeQuery(inventoryCode));

            // Double-check with a small delay to avoid race conditions
            if (existingProduct != null)
            {
                _logger.LogWarning($"Attempt to create duplicate product with inventory code {inventoryCode}");
                throw new DuplicateEntityException($"Product with inventory code {inventoryCode} already exists");
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
                ["departmentId"] = dto.DepartmentId,
                ["isWorking"] = dto.IsWorking,
                ["isActive"] = dto.IsActive,
                ["isNewItem"] = dto.IsNewItem,
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

            // If you want to remove image , send ImageFile as null
            else
            {
                updateData["imageUrl"] = string.Empty;
            }
            
            return updateData;
        }



        public async Task<List<string>> TrackWhatChanges(ProductDto existingProduct, UpdateProductDto updatedProduct)
        {
            ArgumentNullException.ThrowIfNull(existingProduct);
            ArgumentNullException.ThrowIfNull(updatedProduct);

            var changes = new List<string>();
            if (existingProduct.Vendor != updatedProduct.Vendor)
                changes.Add($"Vendor: {existingProduct.Vendor} → {updatedProduct.Vendor}");
            if (existingProduct.Model != updatedProduct.Model)
                changes.Add($"Model: {existingProduct.Model} → {updatedProduct.Model}");
            if (existingProduct.CategoryId != updatedProduct.CategoryId)
                changes.Add($"Category: {existingProduct.CategoryName} → {await GetCategoryNameAsync(updatedProduct.CategoryId)}");
            if (existingProduct.DepartmentId != updatedProduct.DepartmentId)
                changes.Add($"Department: {existingProduct.DepartmentName} → {await GetDepartmentNameAsync(updatedProduct.DepartmentId)}");
            if (existingProduct.Worker != updatedProduct.Worker)
                changes.Add($"Worker: {existingProduct.Worker ?? "None"} → {updatedProduct.Worker ?? "None"}");
            if (existingProduct.Description != updatedProduct.Description)
                changes.Add($"Description: {existingProduct.Description} → {updatedProduct.Description}");
            if (existingProduct.IsNewItem != updatedProduct.IsNewItem)
                changes.Add(updatedProduct.IsNewItem == true ? "Product is new now" : "Product's status changed to old");
            if (existingProduct.IsActive != updatedProduct.IsActive)
                changes.Add(updatedProduct.IsActive == true ? "Product is active now" : "Product is not available");
            if (existingProduct.IsWorking != updatedProduct.IsWorking)
                changes.Add(updatedProduct.IsWorking == true ? "Product is working now" : "Product is not working ");
            if (updatedProduct.ImageFile != null)
                changes.Add("Product image was updated");

            return changes;
        }



        public async Task<string?> GetCategoryNameAsync(int categoryId)
        {
            var categoryName = await _categoryRepository.GetByIdAsync(categoryId)
                ?? throw new NotFoundException($"Category was not found with ID: {categoryId}");
            return categoryName?.Name;
        }



        public async Task<string?> GetDepartmentNameAsync(int departmentId)
        {
            var departmentName = await _departmentRepository.GetByIdAsync(departmentId)
                ?? throw new NotFoundException($"Department was not found with ID: {departmentId}");
            return departmentName.Name;
        }



        public int GetUserId(ClaimsPrincipal User)
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
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